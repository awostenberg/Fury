Fury
====

###Distributed stress test

One or more clients stress the local system under test and stream results to a recording server. 

###Design
First implemented in [F#](http://fsharp.org/) 
using Erlang-style [actor-based concurrency](http://fsharpforfunandprofit.com/posts/concurrency-actor-model) .

###Implementation
New to F#? It's a mature, function-first, programming language
that reads like Ruby or Python with type inference. Learn more at [fsharp.org](http://fsharp.org).
You can try F# "hello world" now from your browser in [http://tryfs.net/](http://tryfs.net/)

The "Syntax in sixty seconds" essay [here](http://fsharpforfunandprofit.com/posts/fsharp-in-60-seconds/) 
will get you fluent enough to read along the code.
I recommend this tour:

- First,  the core messaging loop in [Server.fs](Fury/Server.fs)
- Second, the main program and command line parsing in [Program.fs](Fury/Program.fs)
- Optionally, Erik Tsarpalis' poor man's distributed actor in [Actor.fs](Fury/Actor.fs) 

###How to install and run

####Install Runtime
This runs on Mac, Linux and Windows. Tested on Mac and Ubuntu.

On Mac and Linux you'll need [Mono](http://www.mono-project.com/Main_Page) -- 
the .NET runtime required by F#. 

For Linux, either "apt-get install mono-complete" as described  [here](http://fsharp.org/use/linux/)
or use the one-click executable provided in [releases](https://github.com/awostenberg/Fury/releases)
for Ubuntu 12.04 that has the mono runtime embedded.


####Optional Build
If you use the provided one-click executable there is nothing to build. 
If you want to build from source:

0. git clone this repo and cd into Fury/
0. xbuild Fury.sln

####Run
There is a single executable called Fury.exe which can run as a server or client under Mono.

To start it as the server from a terminal shell type:

    > mono Fury/bin/Debug/Fury.exe -server

Then start one or more named clients, each with 
a chunk size in megabytes,
a file rollover size in megabytes, 
a duration in minutes,
and an output path:

    > mono Fury/bin/Debug/Fury.exe -client Alecto 10 100 1 /tmp
    > mono Fury/bin/Debug/Fury.exe -client Magaera 20 200 1 /tmp
    > mono Fury/bin/Debug/Fury.exe -client Tisiphone 30 300 1 /tmp

When the last client finishes, the server exits, and reports general statistics.

There are several more command line options not shown above, such as IP Host address.
To see usage help, type:

    > mono Fury/bin/Debug/Fury.exe




