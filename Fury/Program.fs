// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

(*
  general design

  build and run instructions
    1. apt-get install mono
    2. git clone repo
    3. xbuild Fury.sln
    4. sh> mono Fury/bin/Debug/program.exe -server
    5. sh1> monmo Fury/bin/Debug.program.exe -client
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


// start the exectuable in "server" or "client" mode, depending on command line


open Server
[<EntryPoint>]
let main argv = 
    printfn "%A" argv
    printfn "%A: Fury on: %A"  (System.DateTime.Now) argv
    printfn "usage: fury  -client  [-to localhost:8090]  -name alecto -duration 30.minutes -chunk 10.mb  -filesys tmp/ -rollover 100.mb"
    printfn "true usage:  fury <clientId> <chunkMb> <durationSeconds>"
    let port = 8091
    let forSeconds = 600.0
    let ep = System.Net.IPEndPoint(System.Net.IPAddress.Parse "127.0.0.1",port)
    if argv.Length > 0 then
      let mb i = (float i) * 1.0<mb>
          // command line parsing, no error checks
      let clientId = System.Int32.Parse argv.[0]
      let chunkSize = System.Int32.Parse argv.[1] |> mb
      let durationSeconds = System.Int32.Parse argv.[2]
      printfn "client %d to port %d chunk %A<mb> duration %d<seconds>" clientId port chunkSize durationSeconds
      let client = Actor.TcpActorClient<Server.Message>(ep)
      let postSlowly msg =
          client.Post msg
          printf "."
          System.Threading.Thread.Sleep (System.TimeSpan.FromSeconds 1.0)
      let testMessages = [Start clientId;ServerReport;Rollover (clientId,chunkSize);Stop clientId]
      postSlowly (Start clientId)

      // inner loop -- rethink as loop not sequence

      let endTimes = System.DateTime.Now + (System.TimeSpan.FromSeconds (float durationSeconds))
      let timeRemaining msg = System.DateTime.Now < endTimes
      [1..durationSeconds] |> List.map (fun i -> Rollover (clientId,chunkSize)) |> List.iter postSlowly
      postSlowly (Stop clientId)
    else
      printfn "server at %d for %f seconds" port forSeconds
      let server = new Actor.TcpActor<Server.Message>(Server.server,ep)
      let duration = System.TimeSpan.FromSeconds forSeconds
      printfn "it runs!...for %A" duration
      System.Threading.Thread.Sleep duration
      printfn "stop server"   // no, should wait on the exit
      server.Stop()
    0 // return an integer exit code
