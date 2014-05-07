module Server

  // for fun I'm trying out message passing style of concurrency, similar to Erlang. No mutable state, so no locks. Just application logic.
  // messages and agents http://fsharpforfunandprofit.com/posts/concurrency-actor-model/
  // In Python I'd use the equivalent (research that) or else good old threads and locks for concurrency on mutable client list

  open Microsoft.FSharp.Control
  open System


  [<Measure>] type mb
  [<Measure>] type seconds

  type ClientState = Green of int<seconds> | Red | Done

  type ClientId = int
  type Client = {id:ClientId;state:ClientState}
  type ServerInfo = {totalMb:float<mb>;clientsServed:int}

  type Message = | Start of ClientId | Stop of ClientId | Rollover of ClientId*float<mb> | Heartbeat of ClientId | ServerExit | ServerTick | ServerReport

  //todo:encapsulate in an object
  let serverInitialState = {totalMb=0.0<mb>;clientsServed=0}
  let server = MailboxProcessor.Start (fun inbox ->
      let rec loop(clients,(serverInfo:ServerInfo)) =
        let rm clientId list = list |> List.filter (fun c -> c.id <> clientId)
        let add client list = client::list      // second thoughts: dictionary
        let replace client list = rm client.id list |> add client
        let isWorking client = match client.state with | Green n -> true | _ -> false
        let working list = list |> List.filter isWorking |> List.length
        async { let! envelope = inbox.Receive()
                let body = envelope
                match body with 
                  | Start client ->
                      printfn "Start client %d" client
                      let newClients = add {id=client;state=Green 10<seconds>} clients
                      let newServerInfo = {serverInfo with clientsServed=serverInfo.clientsServed+1}
                      return! loop(newClients,newServerInfo)
                  | Stop client ->
                      printfn "Stop client %d" client
                      return! loop(rm client clients,serverInfo)
                  | Rollover (client,m) -> 
                    printfn "Rollover %d %A<mb>, waiting..." client serverInfo
                    return! loop(clients,{serverInfo with totalMb=serverInfo.totalMb+m})
                  | Heartbeat client ->
                    printfn "Heartbeat %d" client
                    return! loop(clients,serverInfo)
                  | ServerReport ->
                    clients |> List.iter (fun c -> printfn "%A" c)
                    printfn "total clients %A" serverInfo
                    return! loop(clients,serverInfo)
                  | ServerTick ->
                    // when all clients finished report general stats and finish
                    if serverInfo.clientsServed > 0 && working clients = 0 then 
                      inbox.Post(ServerReport)
                      inbox.Post(ServerExit)
                    return! loop(clients,serverInfo)
                  | ServerExit -> 
                    printfn "server exit"
                    return() 
                    }
      loop ([],{totalMb=0.0<mb>;clientsServed=0}) )

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
