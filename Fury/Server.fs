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
  type ClientState = Green of int<seconds> | Red | Done

  type ClientId = string
  type Client = {id:ClientId;state:ClientState;nRollovers:int}
  type ServerInfo = {totalMb:float<mb>;clientsServed:int}

  // Define the kinds of messages clients send to the server
  // This is a discriminated union -- a kind of lightweight class definition.
  // In Python I'd define an abstract Message, with each type below subtype, and methods to serialize/deserialize polymorphically
  type Message = | Start of ClientId | Stop of ClientId | Rollover of ClientId*float<mb>*int | Heartbeat of ClientId | ServerExit | ServerTick | ServerReport

  // for fun I'm trying out message passing style of concurrency, similar to Erlang. No mutable state, so no locks. Just application logic.
  // messages and agents http://fsharpforfunandprofit.com/posts/concurrency-actor-model/
  // In Python I'd use the equivalent, or else good old threads and locks for concurrency on mutable client list

  let serverInitialState = {totalMb=0.0<mb>;clientsServed=0}

  type ServerAgent() =
    let allDone = new System.Threading.Semaphore(maximumCount=1,initialCount=0) // signals done to the one who launched agent
    let server = MailboxProcessor.Start (fun inbox ->
        let rec loop(clients,(serverInfo:ServerInfo)) =

          let rm clientId list = list |> List.filter (fun c -> c.id <> clientId)
          let add client list = client::list      // second thoughts: dictionary
          let replace client list = rm client.id list |> add client
          let isActive client = match client.state with | Green n -> true | _ -> false
          let active list = list |> List.filter isActive |> List.length

          async { let! envelope = inbox.Receive()
                  let body = envelope
                  match body with 
                    | Start client ->
                        printfn "%s: Start" client
                        let newClients = add {id=client;state=Green 10<seconds>;nRollovers=0} clients
                        let newServerInfo = {serverInfo with clientsServed=serverInfo.clientsServed+1}
                        return! loop(newClients,newServerInfo)
                    | Stop client ->
                        printfn "%s: Stop" client
                        inbox.Post(ServerReport)    // interim report
                        inbox.Post(ServerTick)
                        return! loop(rm client clients,serverInfo)
                    | Rollover (client,mb,rolloverCount) -> 
                      printfn "%s: Rollover %s %d" client (prettyPrint mb) rolloverCount
                      return! loop(clients,{serverInfo with totalMb=serverInfo.totalMb+mb})
                    | Heartbeat client ->
                      printfn "%s: Heartbeat" client
                      return! loop(clients,serverInfo)
                    | ServerReport ->
                      printfn "Master: Total clients served=%d active clients=%d total written=%s " serverInfo.clientsServed (active clients) (prettyPrint serverInfo.totalMb)
                      clients |> List.iter (fun c -> printfn "\t%s\t%A" c.id c.state)
                      return! loop(clients,serverInfo)
                    | ServerTick ->
                      // when all clients finished report general stats and finish
                      if serverInfo.clientsServed > 0 && active clients = 0 then 
                        inbox.Post(ServerReport)
                        inbox.Post(ServerExit)
                      return! loop(clients,serverInfo)
                    | ServerExit -> 
                      printfn "Master: Server exit"
                      allDone.Release() |> ignore
                      return() 
                      }
        loop ([],{totalMb=0.0<mb>;clientsServed=0}) )
    member self.Post msg = server.Post msg
    member self.WaitAllDone() = allDone.WaitOne()
    member self.Mailbox() = server

  // finite state machine in the server modeling state of client based on client heartbeats
  // in Python I'd use the State pattern (http://en.wikipedia.org/wiki/State_pattern) unless there is an idiomatic way
  module ClientFsm =
    type ClientHealth = Green of int<seconds> | Red | Done

    // client heartbeat event... always returns to green state
    let heartbeat s =
        Green 10<seconds>

    // client tick tock event
    let tickSecond s =
        match s with
          | Green countdown when countdown > 0<seconds> -> Green (countdown - 1<seconds>) // can dwell for this long before presumed dead
          | Green countdown -> Red
          | Red -> Red
          | Done -> Done

    module exploreHeartbeat =
      Green 10<seconds> |> heartbeat |> tickSecond |> tickSecond |> heartbeat |> ignore
      Seq.unfold (fun state -> Some(state,tickSecond state) ) (Green 5<seconds>) |> Seq.take 10 |> Seq.toList |> ignore
