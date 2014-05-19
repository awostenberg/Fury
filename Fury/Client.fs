module Client

open Server

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


let zeroArray (size:float<mb>) = 
  let sz = size/1.0<mb>*1000.*1000./8. |> int
  Array.zeroCreate<uint64> sz
let newStopwatch() =
  let stopwatch = new System.Diagnostics.Stopwatch()
  stopwatch.Start()
  stopwatch
type RunSpec = {chunk:int;seed:uint64;rollNth:int;buf:uint64[];didRoll:bool;roller:Rolling.RollingFile;stopwatch:System.Diagnostics.Stopwatch}
let generate (x:RunSpec) =
  let newSeed = Random.fill x.buf x.seed
  {x with seed=newSeed;chunk=x.chunk+1}
let write x = 
  x.roller.Write x.buf
  //printf "%d %A " x.chunk (x.buf.[0])
  x
let rollover (x:RunSpec) = 
  let doRoll = x.chunk % x.rollNth = 0
//    if doRoll then
//      printf "\nfile %d: " (x.chunk/x.rollNth)
  {x with didRoll=doRoll}
let slowDown (x:RunSpec) =
  System.TimeSpan.FromSeconds (1.0) |> System.Threading.Thread.Sleep
  x
let quickTest filePath =
  let rf = Rolling.RollingFile(filePath,3,(fun s -> printfn "file roll %s" s))
  let zeros = zeroArray 10.<mb>
  let sample =  {chunk=0;seed=42UL;rollNth=3;buf=zeros;didRoll=false;roller=rf;stopwatch=newStopwatch()} 
  let chunks = Seq.unfold (fun x -> Some(x,generate x)) sample
  chunks
    |> Seq.take 20
    |> Seq.map rollover
    |> Seq.map write
    |> Seq.iter (fun _ ->())
  rf.Close()

    //Client.quickTest "/tmp/fury-test"

