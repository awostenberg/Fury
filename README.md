Fury
====


###Distributed stress test


One ore more clients stress the local system under test and post results to a recording server. 

###Design
First implemented in [F#](http://fsharp.org/) 
using Erlang-style [actor-based concurrency](http://fsharpforfunandprofit.com/posts/concurrency-actor-model/"Introduction") 
with a translation to Python, planned.

###Implementation
New to F#? It reads like Python or Ruby with type inference. For example, here is a snippet printing the squares of the first 10 numbers:

    let square n = n*n
    List.map square [1..10]
   
You can run the above in F# now from your browser in [http://tryfs.net/](http://tryfs.net/"try it!")

The "Syntax in sixty seconds" essay [here](http://fsharpforfunandprofit.com/posts/fsharp-in-60-seconds/)
will get you fluent enough to read along the code. I recommend:

- First,  the core messaging loop in Server.fs 
- Second, the main program and command line parsing in Program.fs
- Optionally, Erik Tsarpalis's poor man's distributed actor in Actor.fs 


###How to build and run

This runs on Mac, Linux and Windows. Tested on Mac and Ubuntu. 
On Linux you'll need to apt-get install [Mono](http://www.mono-project.com/Main_Page) -- 
the .NET runtime, as described next:


####Build
0. apt-get install mono-complete
0. From the Ubuntu software center GUI install "fsharp" -  [trusty/universe](http://fsharp.org/use/linux/)
0. git clone this repo and cd into Fury/
0. xbuild Fury.sln

####Run
There is a single executable called Fury.exe which can run as a server or client under Mono.
To start it as the server from a terminal shell type:

    > mono Fury/bin/Debug/Fury.exe

Then start one or more numbered clients, each with a chunk size in mb, and a duration in minutes:

    > mono Fury/bin/Debug/Fury.exe 1 10 1
    > mono Fury/bin/Debug/Fury.exe 2 20 1

When the last client finishes, the server exits, and reports general statistics.