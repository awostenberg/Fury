
module Random 
  // fast random number generator with period 2^64-1; generates about 230 mbytes /second on iMac
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
    let elapsed,newseed = time (fun () -> fill buf seed)
    let mbCount = float count / 1000. / 1000.
    let ts = elapsed.TotalSeconds
    mbCount/ts     