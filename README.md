Fury
====


###Distributed stress test


One ore more clients stress the local system under test and post results to a recording server. 

###Design
First implemented in [F#](http://fsharp.org/) 
using Erlang-style [actor-based concurrency](http://fsharpforfunandprofit.com/posts/concurrency-actor-model) 
with a translation to Python, planned.

###Implementation
New to F#? It reads like Python or Ruby with type inference. You can try F# "hello world" now from your browser in [http://tryfs.net/](http://tryfs.net/)

The "Syntax in sixty seconds" essay [here](http://fsharpforfunandprofit.com/posts/fsharp-in-60-seconds/) will get you fluent enough to read along the code.
I recommend this tour:

- First,  the core messaging loop in Server.fs 
- Second, the main program and command line parsing in Program.fs
- Optionally, Erik Tsarpalis's poor man's distributed actor in Actor.fs 


###How to build and run

####Install Runtime
This runs on Mac, Linux and Windows. Tested on Mac and Ubuntu. 

On Mac and Linux you'll need [Mono](http://www.mono-project.com/Main_Page) -- 
the .NET runtime required by F#. For Linux use 
apt-get install as described  [here](http://fsharp.org/use/linux/)

Note: for Ubuntu, not having access to [trust/universe](http://packages.ubuntu.com/trusty/fsharp)
I used the Ubuntu software center GUI installer on the two packages mono-complete and fsharp .


####Build
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