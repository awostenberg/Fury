// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

// Distributed file system stress tester
// See README.MD for detailed run notes. 

open Server

// a simple command line parser
module CommandLine = 
    type ServerConfig = 
      {outDb:string;host:string;port:int}
      member self.endPoint() = System.Net.IPEndPoint(System.Net.IPAddress.Parse self.host,self.port)
    type ClientConfig = 
      {host:string;port:int;clientId:ClientId;chunkSize:float<mb>;duration:int<minutes>;rolloverEvery:float<mb>;outFilePath:string}
      member self.endPoint() = System.Net.IPEndPoint(System.Net.IPAddress.Parse self.host,self.port)
    type Config = Usage | Master of ServerConfig | Slave of ClientConfig
    let usage = """ 
usage:   Fury [-server] | [-client <name> <chunkMb> <rolloverMb> <durationMinutes> <outputDir> ]
example: Fury -server                  # starts the server
         Fury -client Alecto 10 40 1 /tmp  # starts a client writing 10mb chunks, rollover file every 40mb, for 1 minute, to output directory /tmp"""
    let parse (argv:string[]) =
      let argvDefaulted n defaultVal = if n < argv.Length then argv.[n] else defaultVal
      //parse command line args in order: [-server] | [-client <clientId> <chunkMb> <rolloverMb> <durationMinutes>]
      if argv.Length = 0 
        then Usage
        else 
          match argv.[0].ToLower() with
            | "-server" -> 
              Master {outDb="fury.csv";host="127.0.0.1";port=8091} 
            | "-client" -> 
              let mb i = (float i) * 1.0<mb>
              let minutes i = i * 1<minutes>
              Slave {host="127.0.0.1";port=8091;
                      clientId=argvDefaulted 1 "Alecto";
                      chunkSize=System.Int32.Parse (argvDefaulted 2 "10") |> mb;
                      rolloverEvery=System.Int32.Parse (argvDefaulted 3 "40") |> mb;
                      duration=System.Int32.Parse (argvDefaulted 4 "1") |> minutes;
                      outFilePath=argvDefaulted 5 "/tmp"}
            | _ -> Usage
  //ts,count,ts,seed //,seed,b8.[1..4]

// a rolling file that closes and reopens itself after writing up to a configured size
module Rolling =
  type FileStreams = {fs:System.IO.FileStream;binary:System.IO.BinaryWriter}
  type RollingFile(baseFn:string,rolloverEvery:int,traceMsg:(string->unit)) =
      let mutable count = 0
      let fn seq = sprintf "%s.%03d.tmp" baseFn (seq/rolloverEvery)
      let newFile seq = 
          traceMsg (fn seq)
          let fs = new System.IO.FileStream(fn seq,System.IO.FileMode.Create)
          fs.Seek(0L,System.IO.SeekOrigin.Begin) |> ignore
          let bw = new System.IO.BinaryWriter(fs)
          {fs=fs;binary=bw}
      let mutable output = newFile 0
      member this.Close() =
        output.fs.Close()
        output.binary.Close()
      member this.Write(chunk:uint64[]) =
        if count>0 && count%rolloverEvery=0 then 
          this.Close() 
          output <- newFile count
        chunk |> Array.iter (fun x -> output.binary.Write x)
        count <- count + 1
      //todo: add iDispose

module Client =
  let zeroArray (size:float<mb>) = 
    let sz = size/1.0<mb>*1000.*1000./8. |> int
    Array.zeroCreate<uint64> sz

  type RunSpec = {chunk:int;seed:uint64;rollNth:int;buf:uint64[];didRoll:bool;roller:Rolling.RollingFile}
  let generate (x:RunSpec) =
    let newSeed = Random.fill x.buf x.seed
    {x with seed=newSeed;chunk=x.chunk+1}
  let write x = 
    x.roller.Write x.buf
    //printf "%d %A " x.chunk (x.buf.[0])
    x
  let rollover (x:RunSpec) = 
    let doRoll = x.chunk % x.rollNth = 0
    if doRoll then
      printf "\nfile %d: " (x.chunk/x.rollNth)
    {x with didRoll=doRoll}
  let slowDown (x:RunSpec) =
    System.TimeSpan.FromSeconds (1.0) |> System.Threading.Thread.Sleep
    x
  let quickTest filePath =
    let rf = Rolling.RollingFile(filePath,3,(fun s -> printfn "file roll %s" s))
    let zeros = zeroArray 10.<mb>
    let sample =  {chunk=0;seed=42UL;rollNth=3;buf=zeros;didRoll=false;roller=rf} 
    let chunks = Seq.unfold (fun x -> Some(x,generate x)) sample
    chunks
      |> Seq.take 20
      |> Seq.map rollover
      |> Seq.map write
      |> Seq.iter (fun _ ->())
    rf.Close()

    //Client.quickTest "/tmp/fury-test"

open Client

[<EntryPoint>]
let main argv = 
    match CommandLine.parse argv with
      | CommandLine.Usage -> printfn "%s" CommandLine.usage
      | CommandLine.Master config ->
          printfn "Fury server at port %d" config.port
          let serverAgent = new Server.ServerAgent(1<seconds>)
          let server = new Actor.TcpActor<Server.Message>(serverAgent.Mailbox(),config.endPoint())
          serverAgent.WaitAllDone()
          server.Stop()
      | CommandLine.Slave config -> 
          printfn "Fury %s to server at %s chunk %s duration %d<minutes> output %s" 
            config.clientId (config.endPoint().ToString()) (prettyPrint config.chunkSize) (config.duration/1<minutes>) config.outFilePath
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

          // generate and write data in an infinite lazy sequence which ends on time
          let endTimes = System.DateTime.Now.AddMinutes (float config.duration)   
          let nth = int (config.rolloverEvery/config.chunkSize)
          let outFiles = sprintf "%s/%s" config.outFilePath config.clientId
          let rollingFile = new Rolling.RollingFile(outFiles,nth,(fun s -> printfn "rollover %s" s))
          let initialState = {buf=zeroArray config.chunkSize;rollNth=nth;seed=42UL;didRoll=false;roller=rollingFile;chunk=0}
          Seq.unfold (fun x -> Some(x,Client.generate x)) initialState
            |> Seq.takeWhile (fun _ -> System.DateTime.Now < endTimes) 
            |> Seq.map Client.rollover
            |> Seq.map (fun x -> 
              if x.didRoll then (Rollover (config.clientId,config.rolloverEvery,x.chunk)) |> client.Post
              x)
            |> Seq.map Client.write
//            |> Seq.map Client.slowDown 
            |> Seq.iter (fun _ -> ())

          // advise server I am done
          client.Post (Stop config.clientId)
          beatingHeart.CancelAsync()
          ()
    0 // return an integer exit code
