# FSharp.Control.Future
Concurrent programming without fear of exceptions

FSharp.Control.Future provides a set of abstractions for dealing with asynchronous computations that may throw exceptions,
as well as providing a mechanism to handle synchronous and asynchronous values using a uniform API.

A short sample:
```fsharp
open FSharp.Control.Future

// You can treat synchronous values as asyncs without any overhead
let syncFuture = Future.wrap 1

// There are methods for lifting asyncs and tasks to a future
let asyncFuture = Future.ofAsync(async { Async.Sleep 1000; return 1})

// Sometimes we want to encapsulate exceptions
let failedFuture = Future.failed (Exception "Oh No!")

// We can safely apply maps and binds without fear of crashing
let mapped = Future.map (fun x -> x + 1) failedFuture

// And we can also recover values safely
let rescued = Future.recover (fn e -> printfn "We recovered from exception %s" e.message; 1) mapped

// It also comes with a computation expression!
let complicated = future {
  let s = 1
  let! a = Future.ofAsync(async { return 2})
  return s + a }
  
 ```
 
 The library also comes with the `Try<'T>` type, which represents a value that may fail with an exception (Think of it as an `option` with a bit more info)
 It also comes with map, bind, recover, etc...
