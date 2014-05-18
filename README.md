Fury
====

###Distributed stress test

One or more clients stress the local system under test and post results to a recording server. 

###Design
First implemented in [F#](http://fsharp.org/) 
using Erlang-style [actor-based concurrency](http://fsharpforfunandprofit.com/posts/concurrency-actor-model) 
with a translation to Python, planned.

###Implementation
New to F#? It reads like Python or Ruby with type inference. 
You can try F# "hello world" now from your browser in [http://tryfs.net/](http://tryfs.net/)

The "Syntax in sixty seconds" essay [here](http://fsharpforfunandprofit.com/posts/fsharp-in-60-seconds/) 
will get you fluent enough to read along the code.
I recommend this tour:

- First,  the core messaging loop in [Server.fs](Fury/Server.fs)
- Second, the main program and command line parsing in [Program.fs](Fury/Program.fs)
- Optionally, Erik Tsarpalis' poor man's distributed actor in [Actor.fs](Fury/Actor.fs) 


###How to build and run

####Install Runtime
This runs on Mac, Linux and Windows. Tested on Mac and Ubuntu. 

On Mac and Linux you'll need [Mono](http://www.mono-project.com/Main_Page) -- 
the .NET runtime required by F#. For Linux, install as described  [here](http://fsharp.org/use/linux/).


####Build
0. git clone this repo and cd into Fury/
0. xbuild Fury.sln

####Run
There is a single executable called Fury.exe which can run as a server or client under Mono.
To start it as the server from a terminal shell type:

    > mono Fury/bin/Debug/Fury.exe -server

Then start one or more named clients, each with 
a chunk size in mb,
a file rollover size in mb, 
and a duration in minutes:

    > mono Fury/bin/Debug/Fury.exe -client Alecto 10 100 1
    > mono Fury/bin/Debug/Fury.exe -client Magaera 20 200 1
    > mono Fury/bin/Debug/Fury.exe -client Tisiphone 30 300 1

When the last client finishes, the server exits, and reports general statistics.

###Qualifications
0. Message limit crash. There is a problem in the 3rd party distributing messaging library
or my use of it. After about 400 messages the server crashes on my mac: "too many open files".
I suspect it's leaking socket handles.
With 3 clients each writing a heartbeat every 5 seconds and a rollover message every
5 seconds, that translates to about 72 messages/minute or about 5 minutes of runtime. 
Your mileage may vary, because the time-to-crash 
is a function of rollover rate, number of clients, and open file quota.
0. Memory stats. The client should "report CPU and memory info on data thread every 10 seconds".
This is not yet implemented.
0. The client "should complain on startup if the runtime and 'chunk' configuration
do not allow for two rollovers".
This is not yet implemented. I need clarification on a "reasonable" threshold. 
Research [here](http://bit.ly/1lknSqI) indicates high end SSD is 3.2 GBit/second 
but network throughput is 1 GBit/second.
0. The server should "write client performance data to a db". 
This is not yet implemented. But raw data is in the server log. Grep is your friend.
0. System "should run on Ubuntu 12.04".  Verified on Ubuntu 14.04 not yet 12.04.
F# and it's runtime happened to be prebuilt for 14.04 from option 1 [here](http://fsharp.org/use/linux/).
If that doesn't work, consider from that list option 2 or 5.

