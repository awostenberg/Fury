// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

(*
  general design

  build and run instructions
    1. apt-get install mono-complete
    2. install fsharp: from Ubuntu software center GUI
    3. git clone repo
    4. xbuild Fury.sln
    5. sh> mono Fury/bin/Debug/program.exe
    6. in another terminal window sh1> mono Fury/bin/Debug.program.exe 1 10 60
    7. Repeat 6 for as many clients as desired.
*)

module notes =
  (*
    "Fury on.. waiting for the furies to checkin.."
    "Alecto punishing /tmp for 30 minutes at 10 mb per chunk and rolling over every 100 mb"
    "Magaera is punishing ..."
    "Tisiphone is punishing ..."
    "xyzzy will /not/ punish /tmp for 1 minute at 100 mb per chunk   (yoda rule: config does not two rollovers allow)"
    "Tisiphone is finished"
    "Magaera is finished"
    "Alecto is finished"
    "xyzzy missed several heartbeats so presumed dead"
    "general statistics:   Fury   Total chunks     "
    "Writing stats to csv database..."
    "Fury off"
    *)
    let a=42




open Server

module CommandLine = 
    type ServerConfig = {port:int}
    type ClientConfig = {port:int;clientId:int;chunkSize:float<mb>;duration:int<minutes>;host:string}
    type Config = Master of ServerConfig | Slave of ClientConfig
    let parse (argv:string[]) =
      //parse command line args in order: <clientId> <chunkMb> <durationMinutes>.  No errors
      if argv.Length = 0 
        then Master {port=8091} 
        else 
          let mb i = (float i) * 1.0<mb>
          let minutes i = i * 1<minutes>
          Slave {port=8091;clientId=System.Int32.Parse argv.[0];chunkSize=System.Int32.Parse argv.[1] |> mb;duration=System.Int32.Parse argv.[2] |> minutes;host="127.0.0.1"}
// start the exectuable in "server" or "client" mode, depending on command line
[<EntryPoint>]
let main argv = 
    printfn "%A: Fury on: %A"  (System.DateTime.Now) argv
    //printfn "usage: fury  -client  [-to localhost:8090]  -name alecto -duration 30.minutes -chunk 10.mb  -filesys tmp/ -rollover 100.mb"
    printfn "usage:  fury [<clientId> <chunkMb> <durationMinutes>]\n\twith no arguments, starts the server; \n\twith the listed arguments, starts client"
    let port = 8091
    let forSeconds = 600.0
    let ep = System.Net.IPEndPoint(System.Net.IPAddress.Parse "127.0.0.1",port)
    match CommandLine.parse argv with
      | CommandLine.Master config -> 
          printfn "server at port %d" config.port
          let sa = new Server.ServerAgent()
          let server = new Actor.TcpActor<Server.Message>(sa.Mailbox(),ep)
          //System.Threading.Thread.Sleep forTime   // todo: await server signal
          sa.WaitAllDone() |> ignore
          server.Stop()
      | CommandLine.Slave config -> 
          printfn "client to server on port %d chunk %s duration %d<minutes>" config.port (prettyPrint config.chunkSize) (config.duration/1<minutes>)
          let client = Actor.TcpActorClient<Server.Message>(ep)
          let postSlowly msg =
            client.Post msg
            printf "."
            System.Threading.Thread.Sleep (System.TimeSpan.FromSeconds 1.0)
          postSlowly (Start config.clientId)
          let endTimes = System.DateTime.Now + (System.TimeSpan.FromMinutes(float config.duration))
          while System.DateTime.Now < endTimes do
            Rollover (config.clientId,config.chunkSize) |> postSlowly
          postSlowly (Stop config.clientId)
          ()
    0 // return an integer exit code
