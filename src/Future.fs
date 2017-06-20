namespace FSharp.Control

open System
open System.Collections.Generic
open FSharp.Core

/// Represents a value that may fail with an exception.
type Try<'T> =
    | Success of 'T
    | FailedWith of exn

[<RequireQualifiedAccess>]
module Try =

    /// Returns true if the Try is a success, false otherwise
    let isSuccess (t: Try<'T>) = match t with | Success _ -> true | _ -> false

    /// Returns true if the Try is a failure, false otherwise
    let isFailure (t: Try<'T>) = match t with | FailedWith _ -> true | _ -> false
    /// Lifts a value into a Try
    let inline wrap (v: 'T) = Success v
    /// Lifts an exception into a try
    let failed (e: exn) = FailedWith e

    /// Attempts to apply a function to a value
    /// returns a Success if the application was successful
    /// or returns a FailedWith if the function threw an error
    let inline attempt (fn: 'U -> 'T) (v: 'U) =
        try Success(fn v)
        with | e -> FailedWith(e)

    /// Attempts to unwrap a Try type
    /// will throw the contained exception if the Try was a failure
    let get (t: Try<'T>) = 
        match t with
        | Success s -> s
        | FailedWith e -> raise e
    
    /// Unwraps a try type, and applies a recovery function if the Try contains a failure
    let getWith (fn: exn -> 'T) (t: Try<'T>) =  
        match t with
        | Success s -> s
        | FailedWith e -> fn e
    
    /// Returns an empty Try
    let inline empty<'T> = Success(Unchecked.defaultof<'T>)

    /// Returns Some containing the value if the future is a success, otherwise it returns None
    let toOption (t: Try<'T>) =
        match t with
        | Success s -> Some s
        | FailedWith _ -> None

    /// Transforms a Try into a ChoiceOf2
    let toChoice (t: Try<'T>) =
        match t with
        | Success s -> Choice1Of2 s
        | FailedWith e -> Choice2Of2 e
    
    let ofChoice (c: Choice<'T, exn>) =
        match c with
        | Choice1Of2 v -> Success v
        | Choice2Of2 ex -> FailedWith ex

    // Applies the function fs if t is a Success, or, conversely, applies the function fe if t is a FailedWith
    let transform (fs: 'T  -> Try<'U>) (fe: exn -> Try<'U>) (t: Try<'T>) =
        match t with
        | Success s -> fs s
        | FailedWith e -> fe e

    /// Maps the contents of the Try, the function is not applied if the Try contains a failure
    let map (f: 'T -> 'U) (t: Try<'T>) = 
        match t with
        | Success s -> Success(f s)
        | FailedWith e -> FailedWith e


    /// Binds the Try using the binder function, the function is not applied if the Try contains a failure
    let bind (f: 'T -> Try<'U>) (t: Try<'T>) =
        match t with
        | Success s -> f s
        | FailedWith e -> FailedWith e

    

    /// Applies the recovery function to the Try if it contains a failure
    let recover (f: exn -> 'T) (t: Try<'T>) =
        match t with
        | Success s -> Success s
        | FailedWith e -> Success (f e)
    

/// Represents a computation that may be either Synchronous or Non-Synchronous, that also could possibly fail
type Future<'T> =
    | SyncIO of Try<'T>
    | AsyncIO of Async<Try<'T>>

[<RequireQualifiedAccess>]
module Future =

    /// Returns true if the Future contains a synchronous value, otherwise false
    let isSync (future: Future<'T>) = match future with  | SyncIO _ -> true | _ -> false
    
    /// Returns true if the Future contains a asynchronous computation, otherwise false
    let isAsync (future: Future<'T>) = match future with | AsyncIO _ -> true | _ -> false

        
    /// Gets the value wrapped by the Future
    /// If it is an Async computation, it is executed synchronously and the resulting value is returned
    /// If there was an error, the exception will be raised
    let get (future: Future<'T>) = 
        match future with
            | SyncIO v -> Try.get v
            | AsyncIO a -> 
                async {
                    let! r = a
                    return Try.get r
                } |> Async.RunSynchronously
    
    /// Gets the value wrapped by the Future
    /// If it is an Async computation, it is executed synchronously and the resulting value is returned
    /// If there was an error, the recovery function will be applied to the exception
    let getWith (fn: exn -> 'T) (future: Future<'T>) = 
        match future with
            | SyncIO v -> Try.getWith fn v
            | AsyncIO a -> 
                async {
                    let! r = a
                    return Try.getWith fn r
                } |> Async.RunSynchronously

    /// Wraps a synchronous value in a Future
    let inline wrap (v: 'T) = SyncIO(Success(v))

    /// Wraps a synchronous try in a future
    let inline wrapTry (v: Try<'T>) = SyncIO(v)
    /// Wraps an exception in a Future
    let failed (e: exn) = SyncIO(FailedWith(e))


    /// Transforms a task into an asynchronous Future
    let ofTask t = 
        AsyncIO(async {
            let! r = Async.AwaitTask t
            return Try.wrap r
        })

    /// Transforms an async computation into an asynchronous Future
    let ofAsync (a: Async<'T>) = 
        AsyncIO(async{
            let! r = a
            return Try.wrap r
        })
    
    let ofAsyncTry (a: Async<Try<'T>>) =
        AsyncIO(a)

    let ofBeginEnd ()

    /// Transforms a Future into an asynchronous value
    let toAsync = function
        | SyncIO v -> async.Return v
        | AsyncIO a -> a

    /// Constructs an empty Future
    let inline empty<'T> = SyncIO(Try.empty<'T>)



    /// Binds the Future using the binder function to produce a new Future
    let tryBind (f: Try<'T> -> Future<'U>) (future: Future<'T>) =
        match future with
        | SyncIO v -> f v
        | AsyncIO a ->
            AsyncIO(async {
                let! r = a
                match f r with
                | SyncIO v -> return v
                | AsyncIO a -> return! a
            })

    /// Binds the Future using the binder function to produce a new Future
    let bind (f: 'T -> Future<'U>) (future: Future<'T>) =
        tryBind (fun t -> 
            match t with
            | Success s ->
                try f s
                with | e -> failed e
            | FailedWith e -> failed e
        ) future


    /// Maps the value using the provided mapping function, producing a new Future
    let tryMap (f: Try<'T> -> Try<'U>) (future: Future<'T>) =
        match future with
        | SyncIO v -> SyncIO(f v)
        | AsyncIO a ->
            AsyncIO(async{
                let! r = a
                return f r
            })

    /// Maps the value using the provided mapping function, producing a new Future
    let map (f: 'T -> 'U) (future: Future<'T>) = 
        tryMap(fun t ->
            match t with
            | Success s ->
                try Success(f s)
                with | e -> FailedWith e
            | FailedWith e -> FailedWith e 
        ) future
    
    /// Returns a future that is true if the given Future has either a synchronous or asynchronous failure
    let isFailure (future: Future<'T>) =
        tryMap(Try.isFailure >> Try.wrap) future

    /// Applies the recovery function if the containing value was a failure
    let recover (fn: exn -> 'T) (future: Future<'T>) =
        tryMap(Try.recover fn) future

    /// When the future is completed, either through a success or failure, apply the provided function
    let onComplete (fn: Try<'T> -> unit) (future: Future<'T>) = 
        match future with
        | SyncIO v -> fn v
        | AsyncIO a ->
            async {
                let! r = a
                do fn r
            } |> ignore

    /// Converts list of Futures into Future with a list of Try values.
    /// In case when are non-immediate values in provided list, they are 
    /// executed asynchronously, one by one with regard to their order in list.
    /// Returned list maintain order of values.
    /// When one of the futures in the input list is a failure, then the coresponding Try in the output list will also be a failure
    let collectSequential (values: Future<'T> list): Future<Try<'T> list> =
        match values with
        | [] -> SyncIO(Success [])
        | xs when List.exists (isSync >> not) xs ->
            AsyncIO(async {
                let buffer = Array.zeroCreate values.Length
                let values = xs |> List.toArray
                for i = 0 to values.Length - 1 do
                    let v = values.[i]
                    match v with
                    | SyncIO s -> buffer.[i] <- s
                    | AsyncIO a ->
                        let! r = a
                        buffer.[i] <- r
                return buffer |> Array.toList |> Success
            })
        | xs -> SyncIO (values |> List.map (fun (SyncIO v) -> v) |> Success)

    /// Converts list of Futures into Future with a list of Try values.
    /// When there are non-immediate values, they are batched together and executed in parallel,
    /// in an unordered fashion. Order of values in the returned list is maintained
    /// When one of the futures in the input list is a failure, then the coresponding Try in the output list will also be a failure
    let collectParallel (values: Future<'T> list): Future<Try<'T> list> =
        match values with
        | [] -> SyncIO(Success [])
        | xs ->
            let indexes = List<int>(0)
            let continuations = List<Async<Try<'T>>>(0)
            let buffer = Array.zeroCreate values.Length
            let values = xs |> List.toArray
            for i = 0 to values.Length - 1 do
                let v = values.[i]
                match v with
                | SyncIO s -> buffer.[i] <- s
                | AsyncIO a ->
                    indexes.Add i
                    continuations.Add a
            if indexes.Count = 0 then SyncIO (buffer |> Array.toList |> Success)
            else AsyncIO(async{
                let! vals = continuations |> Async.Parallel
                for i = 0 to indexes.Count - 1 do
                    buffer.[indexes.[i]] <- vals.[i] 
                return buffer |> Array.toList |> Success
            })


            
type FutureBuilder () =
    member x.Zero () = Future.empty
    member x.Return v = Future.wrap v
    member x.ReturnFrom (v: Future<_>) = v
    member x.Bind (v: Future<'T>, binder: 'T -> Future<'U>) = Future.bind binder v


[<AutoOpen>]
module FutureExtensions =
    let future = FutureBuilder ()