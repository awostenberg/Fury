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

  let secondsPerMinute = 60<seconds>/1<minutes> //todo: as member on seconds
  let prettyPrint mb = sprintf "%d<mb>" (int(mb/1.0<mb>))

  // To model the client heartbeat health I'll encode a state machine in a discriminated union
  // in Python I'd might use the State pattern http://en.wikipedia.org/wiki/State_pattern

  module ClientFsm =
    type ClientHealth = Green of int<seconds> | Red | Done

    // client heartbeat event... always returns to green state
    let heartbeat () =
        Green 10<seconds>

    // client tick tock event
    let tick s decrement =
        match s with
          | Green countdown when countdown > 0<seconds> -> Green (countdown - decrement) // can dwell for this long before presumed dead
          | Green countdown -> Red
          | Red -> Red
          | Done -> Done


  // state of client as known in the server
  type ClientId = string
  type Client = 
    {id:ClientId;mutable state:ClientFsm.ClientHealth}
    member self.heartbeat() = self.state <- ClientFsm.heartbeat() 
    member self.tick(decrement) = self.state <- ClientFsm.tick self.state decrement 


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
    let ticker = new System.ComponentModel.BackgroundWorker(WorkerSupportsCancellation=true) // heartbeat signal
    let server = MailboxProcessor.Start (fun inbox ->
        let rec loop(clients,(serverInfo:ServerInfo)) =
          let rm clientId list = list |> List.filter (fun c -> c.id <> clientId)
          let add client list = client::list      // second thoughts: dictionary
          //let replace client list = rm client.id list |> add client
          let isActive client = match client.state with | ClientFsm.Green n -> true | _ -> false
          let active list = list |> List.filter isActive |> List.length
          async { let! envelope = inbox.Receive()
                  let body = envelope
                  match body with 
                    | Start client ->
                        printfn "%s: Start" client
                        let newClients = add {id=client;state=ClientFsm.heartbeat()} clients
                        let newServerInfo = {serverInfo with clientsServed=serverInfo.clientsServed+1}
                        return! loop(newClients,newServerInfo)
                    | Stop client ->
                        printfn "%s: Stop" client
                        inbox.Post(ServerReport)    // interim report
                        inbox.Post(ServerTick 1<seconds>)
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
                      printfn "Master: Total clients served=%d active clients=%d total written=%s " serverInfo.clientsServed (active clients) (prettyPrint serverInfo.totalMb)
                      clients |> List.iter (fun c -> printfn "\t%s\t%A" c.id c.state)
                      return! loop(clients,serverInfo)
                    | ServerTick hbFrequency ->
                      printf "."
                      clients |> List.iter (fun c -> c.tick hbFrequency )
                      //let clients' = clients |> List.map (fun c -> {c with state=(ClientFsm.tick c.state hbFrequency)})
                      // when all clients finished report general stats and finish
                      if serverInfo.clientsServed > 0 && active clients = 0 then 
                        inbox.Post(ServerReport)
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



