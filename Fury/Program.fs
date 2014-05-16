// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

(*
  general design

  build and run instructions
    1. install F# and it's runtime http://fsharp.org/use/linux/
    2. git clone repo
    3. xbuild Fury.sln
    4. mono Fury/bin/Debug/Fury.exe &
    5. mono Fury/bin/Debug/Fury.exe Alecto 10 1   &
    6. Repeat 6 for as many clients as desired.
*)


(*
  "Fury on.. waiting for the furies to checkin.."
  "Alecto punishing /tmp for 30 minutes at 10 mb per chunk and rolling over every 100 mb"
  "Magaera is punishing ..."
  " is punishing ..."
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
      {host:string;port:int;clientId:ClientId;chunkSize:float<mb>;duration:int<minutes>;rolloverEvery:float<mb>}
      member self.endPoint() = System.Net.IPEndPoint(System.Net.IPAddress.Parse self.host,self.port)
    type Config = Usage | Master of ServerConfig | Slave of ClientConfig
    let usage = """ 
usage:   Fury [-server] | [-client <name> <chunkMb> <rolloverMb> <durationMinutes> ]
example: Fury -server                  # starts the server
         Fury -client Alecto 10 40 1   # starts a client writing 10mb chunks, rollover file every 40mb, for 1 minute"""

    let parse (argv:string[]) =
      //parse command line args in order: [-server] | [-client <clientId> <chunkMb> <durationMinutes>]
      if argv.Length = 0 
        then Usage
        else 
          match argv.[0].ToLower() with
            | "-server" -> Master {host="127.0.0.1";port=8091} 
            | "-client" -> 
              let mb i = (float i) * 1.0<mb>
              let minutes i = i * 1<minutes>
              Slave {host="127.0.0.1";port=8091;
                      clientId=argv.[1];
                      chunkSize=System.Int32.Parse argv.[2] |> mb;
                      rolloverEvery=System.Int32.Parse argv.[3] |> mb;
                      duration=System.Int32.Parse argv.[4] |> minutes}
            | _ -> Usage

module Random = 
  // fast random number generator with period 2^64-1; about generates 230 mbytes /second on iMac
  // -- see http://en.wikipedia.org/wiki/Xorshift  
  let nextRand x =
    let x = x ^^^ (x >>> 12)
    let x = x ^^^ (x <<< 25)
    let x = x ^^^ (x >>> 27)
    x * 2685821657736338717UL
  
  /// fill buffer with random numbers generated from seed,
  /// and return the seed to use next time
  let fill (buf:uint64[]) seed =
    let mutable n = seed
    for i in 0..(buf.Length - 1) do
      n <- nextRand n
      buf.[i] <- n
    n

  /// time an operation
  let time fn =
    let w = System.Diagnostics.Stopwatch()
    w.Start()
    let retval = fn()
    w.Stop()
    w.Elapsed,retval

  /// time the generator in mb/seconds
  let timeTheGenerator() =
    let count = 1000*1000*10
    let buf = Array.zeroCreate<uint64> (count/8)
    let seed = (uint64 System.DateTime.Now.Ticks)
    let t,newseed = time (fun () -> fill buf seed)
    let ts = t.TotalSeconds*1.<seconds>
    let mbCount = float count /1000./1000.*1.<mb>
    mbCount/ts       //ts,count,ts,seed //,seed,b8.[1..4]

module Client =
  let zeroArray (size:float<mb>) = 
    let sz = size/1.0<mb>*1000.*1000./8. |> int
    Array.zeroCreate<uint64> sz
  let zeros = zeroArray 10.<mb>                       // mutable so if ever >1 data thread need 1/thread

  type RunSpec = {chunk:int;seed:uint64;rollNth:int;buf:uint64[];didRoll:bool}
  let sample =  {chunk=0;seed=42UL;rollNth=3;buf=zeros;didRoll=false} 
  let generate x =
    let newSeed = Random.fill x.buf x.seed
    {x with seed=newSeed;chunk=x.chunk+1}
  let write x = 
    printf "%d %A " x.chunk (x.buf.[0])
    x
  let rollover x = 
    let doRoll = x.chunk % x.rollNth = 0
    if doRoll then
      printf "\nfile %d: " (x.chunk/x.rollNth)
    {x with didRoll=doRoll}
  let slowDown x =
    System.TimeSpan.FromSeconds (1.0) |> System.Threading.Thread.Sleep
    x

  let quickTest() =
    let chunks = Seq.unfold (fun x -> Some(x,generate x)) sample
    chunks
      |> Seq.take 20
      |> Seq.map rollover
      |> Seq.map write
      |> Seq.iter (fun _ ->())

[<EntryPoint>]
let main argv = 
    //printfn "usage: fury  -client  [-to localhost:8090]  -name alecto -duration 30.minutes -chunk 10.mb  -filesys tmp/ -rollover 100.mb"
    match CommandLine.parse argv with
      | CommandLine.Usage -> printfn "%s" CommandLine.usage
      | CommandLine.Master config ->
          printfn "Fury server at port %d" config.port
          let serverAgent = new Server.ServerAgent(1<seconds>)
          let server = new Actor.TcpActor<Server.Message>(serverAgent.Mailbox(),config.endPoint())
          serverAgent.WaitAllDone()
          server.Stop()
      | CommandLine.Slave config -> 
          printfn "Fury %s to server at %s chunk %s duration %d<minutes>" config.clientId (config.endPoint().ToString()) (prettyPrint config.chunkSize) (config.duration/1<minutes>)
          let client = Actor.TcpActorClient<Server.Message>(config.endPoint())
          client.Post (Start config.clientId)
          // heartbeat to tell server I am alive, even if slow
          let heartbeatFrequency = 5<seconds>
          let beatingHeart = new System.ComponentModel.BackgroundWorker(WorkerSupportsCancellation=true)
          beatingHeart.DoWork.Add(fun args -> 
            while beatingHeart.CancellationPending |> not do
              System.Threading.Thread.Sleep (System.TimeSpan.FromSeconds (float(heartbeatFrequency/1<seconds>)))
              client.Post(Heartbeat config.clientId))
          beatingHeart.RunWorkerAsync()

          let endTimes = System.DateTime.Now.AddMinutes (float config.duration)   
          let starter = {Client.sample with buf=Client.zeroArray config.chunkSize;rollNth=int (config.rolloverEvery/config.chunkSize)}
          Seq.unfold (fun x -> Some(x,Client.generate x)) starter
            |> Seq.takeWhile (fun _ -> System.DateTime.Now < endTimes) 
            |> Seq.map Client.rollover
            |> Seq.map (fun x -> 
              if x.didRoll then (Rollover (config.clientId,config.chunkSize,x.chunk)) |> client.Post
              x)
            |> Seq.map Client.write
            |> Seq.map Client.slowDown 
            |> Seq.iter (fun _ -> ())

          // advise server I am done
          client.Post (Stop config.clientId)
          beatingHeart.CancelAsync()
          ()
    0 // return an integer exit code
