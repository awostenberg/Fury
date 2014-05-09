Fury
====


###Distributed stress test

One more clients stress the local system under test and post results to a recording server. 
Implemented in F# using [actor-based concurrency](http://fsharpforfunandprofit.com/posts/concurrency-actor-model/"Title").


###Build and run instructions

1. apt-get install mono-complete
2. install fsharp: from Ubuntu software center GUI
3. git clone this repo
4. xbuild Fury.sln
5. sh> mono Fury/bin/Debug/program.exe
6. in another terminal window sh1> mono Fury/bin/Debug.program.exe 1 10 60
7. Repeat 6 for as many clients as desired.
