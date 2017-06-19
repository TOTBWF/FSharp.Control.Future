namespace Future

open System
open System.Collections.Generic
open FSharp.Core

type Try<'T> =
    | Success of 'T
    | FailedWith of exn

[<RequireQualifiedAccess>]
module Try =
    let inline wrap (v: 'T) = Success v
    let failed (e: exn) = FailedWith e

    let inline attempt (fn: 'U -> 'T) (v: 'U) =
        try Success(fn v)
        with | e -> FailedWith(e)

    let get (t: Try<'T>) = 
        match t with
        | Success s -> s
        | FailedWith e -> raise e
    
    let inline empty<'T> = Success(Unchecked.defaultof<'T>)

    let map (f: 'T -> 'U) (t: Try<'T>) = 
        match t with
        | Success s -> Success(f s)
        | FailedWith e -> FailedWith e

    let bind (f: 'T -> Try<'U>) (t: Try<'T>) =
        match t with
        | Success s -> f s
        | FailedWith e -> FailedWith e
    
    let recover (f: exn -> 'T) (t: Try<'T>) =
        match t with
        | Success s -> Success s
        | FailedWith e -> Success (f e)
    

type Future<'T> =
    | SyncIO of Try<'T>
    | AsyncIO of Async<Try<'T>>

[<RequireQualifiedAccess>]
module Future =

    let isSync (future: Future<'T>) = match future with  | SyncIO _ -> true | _ -> false
    let isAsync (future: Future<'T>) = match future with | AsyncIO _ -> true | _ -> false
        
    let get (future: Future<'T>) = 
        let body = function
            | Success s -> s
            | FailedWith e -> raise e
        match future with
            | SyncIO v -> body v
            | AsyncIO a -> 
                async {
                    let! r = a
                    return body r
                } |> Async.RunSynchronously

    let inline wrap (v: 'T) = SyncIO(Success(v))

    let failed (e: exn) = SyncIO(FailedWith(e))
    let ofTask t = 
        AsyncIO(async {
            let! r = Async.AwaitTask t
            return Try.wrap r
        })

    let ofAsync (a: Async<'T>) = 
        AsyncIO(async{
            let! r = a
            return Try.wrap r
        })

    let toAsync = function
        | SyncIO v -> async.Return v
        | AsyncIO a -> a

    let inline empty<'T> = SyncIO(Try.empty<'T>)

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

    let bind (f: 'T -> Future<'U>) (future: Future<'T>) =
        tryBind (fun t -> 
            match t with
            | Success s ->
                try f s
                with | e -> failed e
            | FailedWith e -> failed e
        ) future


    let tryMap (f: Try<'T> -> Try<'U>) (future: Future<'T>) =
        match future with
        | SyncIO v -> SyncIO(f v)
        | AsyncIO a ->
            AsyncIO(async{
                let! r = a
                return f r
            })

    let map (f: 'T -> 'U) (future: Future<'T>) = 
        tryMap(fun t ->
            match t with
            | Success s ->
                try Success(f s)
                with | e -> FailedWith e
            | FailedWith e -> FailedWith e 
        ) future

    let recover (fn: exn -> 'T) (future: Future<'T>) =
        match future with
        | SyncIO v -> SyncIO(Try.recover fn v)
        | AsyncIO a ->
            AsyncIO(async{
                let! r = a
                return Try.recover fn r
            })

    let onComplete (fn: Try<'T> -> unit) (future: Future<'T>) = 
        match future with
        | SyncIO v -> fn v
        | AsyncIO a ->
            async {
                let! r = a
                do fn r
            } |> ignore


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