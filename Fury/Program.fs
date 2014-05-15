// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

(*
  general design

  build and run instructions
    1. install F# and it's runtime http://fsharp.org/use/linux/
    2. git clone repo
    3. xbuild Fury.sln
    4. mono Fury/bin/Debug/program.exe &
    5. mono Fury/bin/Debug.program.exe Alecto 10 60   &
    6. Repeat 6 for as many clients as desired.
*)


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


open Server

module CommandLine = 
    type ServerConfig = 
      {host:string;port:int}
      member self.endPoint() = System.Net.IPEndPoint(System.Net.IPAddress.Parse self.host,self.port)
    type ClientConfig = 
      {host:string;port:int;clientId:ClientId;chunkSize:float<mb>;duration:int<minutes>}
      member self.endPoint() = System.Net.IPEndPoint(System.Net.IPAddress.Parse self.host,self.port)
    type Config = Master of ServerConfig | Slave of ClientConfig
    let usage = """ 
usage:   Fury [<clientId> <chunkMb> <durationMinutes>]
         with no arguments, starts the server;
         with the 3 listed arguments, starts a client"""

    let parse (argv:string[]) =
      //parse command line args in order: <clientId> <chunkMb> <durationMinutes>.  No errors
      if argv.Length = 0 
        then Master {host="127.0.0.1";port=8091} 
        else 
          let mb i = (float i) * 1.0<mb>
          let minutes i = i * 1<minutes>
          Slave {host="127.0.0.1";port=8091;clientId=argv.[0];chunkSize=System.Int32.Parse argv.[1] |> mb;duration=System.Int32.Parse argv.[2] |> minutes}

// start the exectuable in "server" or "client" mode, depending on command line
[<EntryPoint>]
let main argv = 
    printfn "%A: Fury on: %A"  (System.DateTime.Now) argv
    printfn "%s" CommandLine.usage
    //printfn "usage: fury  -client  [-to localhost:8090]  -name alecto -duration 30.minutes -chunk 10.mb  -filesys tmp/ -rollover 100.mb"
    match CommandLine.parse argv with
      | CommandLine.Master config -> 
          printfn "server at port %d" config.port
          let serverAgent = new Server.ServerAgent(1<seconds>)
          let server = new Actor.TcpActor<Server.Message>(serverAgent.Mailbox(),config.endPoint())
          serverAgent.WaitAllDone()
          server.Stop()
      | CommandLine.Slave config -> 
          printfn "client %s to server on port %d chunk %s duration %d<minutes>" config.clientId config.port (prettyPrint config.chunkSize) (config.duration/1<minutes>)
          let client = Actor.TcpActorClient<Server.Message>(config.endPoint())
          client.Post (Start config.clientId)

          // heartbeat to tell server I am alive, even if slow
          let heartbeatFrequency = 5<seconds>
          let beatingHeart = new System.ComponentModel.BackgroundWorker()
          beatingHeart.DoWork.Add(fun args -> 
            while beatingHeart.CancellationPending |> not do
              System.Threading.Thread.Sleep (System.TimeSpan.FromSeconds (float(heartbeatFrequency/1<seconds>)))
              client.Post(Heartbeat config.clientId))
          beatingHeart.RunWorkerAsync()


          let postSlowly msg =
            System.Threading.Thread.Sleep (System.TimeSpan.FromSeconds (config.chunkSize/10.0<mb>)) //delay proportional to chunk size
            client.Post msg
            printf "."

          // generate and write chunks for the configured duration of the run
          let endTimes = System.DateTime.Now.AddMinutes (float config.duration)
          let mockWriter rc = Rollover (config.clientId,config.chunkSize,rc) |> postSlowly
          Seq.initInfinite id 
            |> Seq.takeWhile (fun _ -> System.DateTime.Now < endTimes) 
            |> Seq.iter mockWriter

          // advise server I am done
          client.Post (Stop config.clientId)
          beatingHeart.CancelAsync()
          ()
    0 // return an integer exit code
