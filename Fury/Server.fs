module Server

  // Welcome to Fury: a tool to "compare performance of various storage solutions on customer network"

  // This first cut is in idomatic F#. F# is a function-first multiparadigm language resembling Python with type inferencing.
  //    see http://fsharp.org/ .   Made with Mono on my iMac. For one-click deployment to Linux see readme.txt http://monodevelop.com/

  // Like you, gentle reader, I think in code. Currently I am thinking functionally in F#. 
  // I intend to translate into Python as part II and will narrate along in coments how that would go


  open Microsoft.FSharp.Control
  open System

  // Since requirements have dimensioned units such as megabytes and times, 
  // I'll define some units of measure to avoid silly math mistakes 
  // like the one that crashed the mars probe http://www.wired.com/2010/11/1110mars-climate-observer-report/
  //   See http://blogs.msdn.com/b/andrewkennedy/
  // in Python I'd apply the whole value pattern http://c2.com/ppr/checks.html#1
  [<Measure>] type mb
  [<Measure>] type seconds
  [<Measure>] type minutes
  [<Measure>] type gb


  let mbPerGb = 1000<mb>/1<gb>
  let secondsPerMinute = 60<seconds>/1<minutes>   //todo: as method on seconds
  let prettyPrint mb = sprintf "%d<mb>" (int(mb/1.0<mb>))

  // todo: what is a reasonable throughput for the "runtime and chunk size configured allow for 2 rollovers"?
  let reasonableUpperThroughputThreshold = 10<gb>/1<seconds>    //thunderbolt - see http://bit.ly/1lknSqI

  // To model the client heartbeat health I'll encode a state machine in a discriminated union
  // in Python I'd might use the State pattern http://en.wikipedia.org/wiki/State_pattern

  module ClientFsm =
    type ClientHealth = Green of int<seconds> | Red | Done

    // client heartbeat event... always returns to green state
    let heartbeat () =
        Green 10<seconds>

    // client tick tock event
    let tick s decrement greenToRedAction =
        match s with
          | Green countdown when countdown > 0<seconds> -> Green (countdown - decrement) // can dwell for this long before presumed dead
          | Green countdown -> greenToRedAction();Red
          | Red -> Red
          | Done -> Done


  // state of client as known in the server
  type ClientId = string
  type Client = 
    {id:ClientId;mutable state:ClientFsm.ClientHealth}
    member self.heartbeat() = self.state <- ClientFsm.heartbeat() 
    member self.tick(decrement,greenToRedAction) = self.state <- ClientFsm.tick self.state decrement greenToRedAction


  // summary statistics of the server
  type ServerInfo = {totalMb:float<mb>;clientsServed:int}

  // Define the kinds of messages clients send to the server
  // This is a discriminated union -- a kind of lightweight class definition.
  // In Python I'd define an abstract Message, with each type below subtype, and methods to serialize/deserialize polymorphically
  type Message = | Start of ClientId | Stop of ClientId | Rollover of ClientId*float<mb>*int | Heartbeat of ClientId | ServerExit | ServerTick of int<seconds> | ServerReport

  // for fun I'm trying out message passing style of concurrency, similar to Erlang. No mutable state, so no locks. Just application logic.
  // messages and agents http://fsharpforfunandprofit.com/posts/concurrency-actor-model/
  // In Python I'd use the equivalent, or else good old threads and locks for concurrency on mutable client list

  let serverInitialState = {totalMb=0.0<mb>;clientsServed=0}

  type ServerAgent(heartbeatFrequency) =
    let allDone = new System.Threading.Semaphore(maximumCount=1,initialCount=0) // signals done to the one who launched agent
    let ticker = new System.ComponentModel.BackgroundWorker(WorkerSupportsCancellation=true) // tick-tock source
    let server = MailboxProcessor.Start (fun inbox ->
        let rec loop(clients,(serverInfo:ServerInfo)) =
          let rm clientId list = list |> List.filter (fun c -> c.id <> clientId)
          let add client list = client::list      // second thoughts: dictionary
          let isActive client = match client.state with | ClientFsm.Green n -> true | _ -> false
          let ativeCountOf list = list |> List.filter isActive |> List.length
          async { let! msg = inbox.Receive()
                  match msg with 
                    | Start client ->
                        printfn "%s: Start" client
                        let newClients = add {id=client;state=ClientFsm.heartbeat()} clients
                        let newServerInfo = {serverInfo with clientsServed=serverInfo.clientsServed+1}
                        return! loop(newClients,newServerInfo)
                    | Stop client ->
                        printfn "%s: Stop" client
                        inbox.Post(ServerReport)    // interim report
                        return! loop(rm client clients,serverInfo)
                    | Rollover (client,mb,iteration) -> 
                      printfn "%s: Rollover %s %d" client (prettyPrint mb) iteration
                      clients |> List.iter (fun c -> if c.id = client then c.heartbeat())
                      return! loop(clients,{serverInfo with totalMb=serverInfo.totalMb+mb})
                    | Heartbeat client ->
                      printfn "%s: Heartbeat" client
                      clients |> List.iter (fun c -> if c.id = client then c.heartbeat())
                      return! loop(clients,serverInfo)
                    | ServerReport ->
                      printfn "Master: Total clients served=%d active clients=%d total written=%s " serverInfo.clientsServed (ativeCountOf clients) (prettyPrint serverInfo.totalMb)
                      clients |> List.iter (fun c -> printfn "\t%s\t%A" c.id c.state)
                      return! loop(clients,serverInfo)
                    | ServerTick hbFrequency ->
                      clients |> List.iter (fun c -> c.tick(hbFrequency,(fun() -> printfn "%s: condition RED -- missed heartbeats" c.id)))
                      // when all clients finished report general stats and finish
                      if serverInfo.clientsServed > 0 && ativeCountOf clients = 0 then 
                        inbox.Post(ServerReport)    // final report
                        inbox.Post(ServerExit)
                      return! loop(clients,serverInfo)
                    | ServerExit -> 
                      printfn "Master: Server exit"
                      ticker.CancelAsync()
                      allDone.Release() |> ignore
                      return() 
                      }
        loop ([],{totalMb=0.0<mb>;clientsServed=0}) )
    do
        ticker.DoWork.Add(fun args -> 
          while ticker.CancellationPending |> not do
            System.Threading.Thread.Sleep (System.TimeSpan.FromSeconds (float(heartbeatFrequency/1<seconds>)))
            server.Post(ServerTick heartbeatFrequency))
        ticker.RunWorkerAsync()
    member self.Post msg = server.Post msg
    member self.WaitAllDone() = allDone.WaitOne() |> ignore
    member self.Mailbox() = server



