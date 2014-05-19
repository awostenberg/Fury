// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

// Distributed file system stress tester
// See README.MD for detailed run notes. 

open Server

// a simple command line parser
module CommandLine = 
    let endPointOf host port =
      let ipa = (System.Net.Dns.GetHostAddresses host).[0]
      System.Net.IPEndPoint(ipa,port)
    type ServerConfig = 
      {outDb:string;host:string;port:int}
      member self.endPoint() = endPointOf self.host self.port
    type ClientConfig = 
      {host:string;port:int;clientId:ClientId;chunkSize:float<mb>;duration:int<minutes>;rolloverEvery:float<mb>;outFilePath:string;seed:uint64}
      member self.endPoint() = endPointOf self.host self.port
    type Config = Usage | Master of ServerConfig | Slave of ClientConfig
    let usage = """ 
usage:   Fury [-server] [port] | [-client [<name> <chunkMb> <rolloverMb> <durationMinutes> <outputDir> <host> <port> <seed>]]
example: Fury -server 8091              # starts the server on port 8091
         Fury -client Alecto 10 40 1 /tmp localhost 8091  42   # starts a client 
                      writing 10mb chunks, rollover file every 40mb, for 1 minute, 
                      to output directory /tmp  with server at localhost, on port 8091, 
                      random number generator seed of 42 (can be any non-zero integer; default is system clock)
         Note: positional parameters may be omitted, in which case they take the defaults given above
         """
    let parse (argv:string[]) =
      let argvDefaulted n defaultVal = if n < argv.Length then argv.[n] else defaultVal
      //parse command line args in positional order with defaults for omitted positional parameters
      if argv.Length = 0 
        then Usage
        else 
          match argv.[0].ToLower() with
            | "-server" -> 
              Master {outDb="fury.csv";host="127.0.0.1";port=System.Int32.Parse (argvDefaulted 1 "8091")} 
            | "-client" -> 
              let mb i = (float i) * 1.0<mb>
              let minutes i = i * 1<minutes>
              let randomSeed = System.DateTime.Now.Ticks |> string
              Slave { clientId=argvDefaulted 1 "Alecto";
                      chunkSize=argvDefaulted 2 "10" |> System.Int32.Parse |> mb;
                      rolloverEvery=argvDefaulted 3 "40" |> System.Int32.Parse  |> mb;
                      duration=argvDefaulted 4 "1" |> System.Int32.Parse |> minutes;
                      outFilePath=argvDefaulted 5 "/tmp";
                      host=argvDefaulted 6 "localhost";
                      port=argvDefaulted 7 "8091" |> System.Int32.Parse
                      seed=argvDefaulted 8 randomSeed  |> System.UInt64.Parse }
            | _ -> Usage

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
          printfn "Fury %s to server at %s chunk %s duration %d<minutes> output %s seed %d" 
            config.clientId (config.endPoint().ToString()) (prettyPrint config.chunkSize) (config.duration/1<minutes>) config.outFilePath config.seed
          let client = Actor.TcpActorClient<Server.Message>(config.endPoint())
          let postAndLog msg =
            client.Post msg
            printfn "%A\t%A" System.DateTime.Now msg
          postAndLog (Start config.clientId)
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
          let rollingFile = new Rolling.RollingFile(outFiles,nth,(fun s -> printfn "file %s" s))
          let initialState = {buf=zeroArray config.chunkSize;rollNth=nth;seed=config.seed;didRoll=false;roller=rollingFile;chunk=0;stopwatch=newStopwatch()}
          Seq.unfold (fun x -> Some(x,Client.generate x)) initialState
            |> Seq.takeWhile (fun _ -> System.DateTime.Now < endTimes) 
            |> Seq.map Client.rollover
            |> Seq.map (fun x -> 
              if x.didRoll then 
                (Rollover (config.clientId,config.rolloverEvery,x.chunk,x.stopwatch.Elapsed)) |> postAndLog
                x.stopwatch.Restart()
              x)
            |> Seq.map Client.write
//            |> Seq.map Client.slowDown 
            |> Seq.iter (fun _ -> ())

          // advise server I am done
          postAndLog (Stop config.clientId)
          beatingHeart.CancelAsync()
          ()
    0 // return an integer exit code
