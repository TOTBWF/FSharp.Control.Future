module Tests

open System
open Expecto
open Future

let basicTests =
  testList "Basic Future Tests" [
    test "Future handles constant values" {
      let f = Future.wrap 1
      Expect.isTrue (Future.isSync f) "Value is synchronous"
      Expect.isFalse (Future.isAsync f) "Value is not asynchronous"
      Expect.equal (Future.get f) 1 "The retrived value is the same"
    }
    test "Future handles async values" {
      let f = Future.ofAsync(async { return 1 })
      Expect.isTrue (Future.isAsync f) "Value is asynchronous"
      Expect.isFalse (Future.isSync f) "Value is not synchronous"
      Expect.equal (Future.get f) 1 "The retrived value is the same"
    }
    test "Future handles errors" {
      let f = Future.failed (Exception "Test Exception")
      Expect.throws (fun () -> Future.get f |> ignore) "Throws the exception when get is used"
    }
  ]

let monadTests =
  testList "Test Monadic Operations" [
    test "Map applies to synchronous inner value" {
      let f = Future.wrap 1
      let g = Future.map((+) 1) f
      Expect.isTrue (Future.isSync g) "Value is synchronous"
      Expect.isFalse (Future.isAsync g) "Value is not asynchronous"
      Expect.equal (Future.get g) 2 "The retrived value had the function applied"
    }

    test "Map applies to asynchronous inner value" {
      let f = Future.ofAsync(async { return 1 })
      let g = Future.map((+) 1) f
      Expect.isTrue (Future.isAsync g) "Value is asynchronous"
      Expect.isFalse (Future.isSync g) "Value is not synchronous"
      Expect.equal (Future.get g) 2 "The retrived value had the function applied"
    }

    test "Map properly handles errors when applied to synchronous values" {
      let f = Future.wrap 1
      let error = Failure "Oh no!"
      let g = Future.map(fun v -> raise error) f
      Expect.isFalse (Future.isAsync g) "Value is asynchronous"
      Expect.isTrue (Future.isSync g) "Value is synchronous"
    }

    test "Map properly handles errors when applied to asynchronous values" {
      let f = Future.ofAsync(async { return 1 })
      let error = Failure "Oh no!"
      let g = Future.map(fun v -> raise error) f
      Expect.isTrue (Future.isAsync g) "Value is asynchronous"
      Expect.isFalse (Future.isSync g) "Value is not synchronous"
    }
  ]
let computationTests =
  testList "Computation Expression Tests" [
    test "Future computation allows return of constant values" {
      let f = future { return 1 }
      Expect.isTrue (Future.isSync f) "Value is synchronous"
      Expect.isFalse (Future.isAsync f) "Value is not asynchronous"
      Expect.equal (Future.get f) 1 "The retrived value is the same"
    }

    test "Future computation allows return of future values" {
      let f = future { return! Future.ofAsync(async { return 1}) }
      Expect.isTrue (Future.isAsync f) "Value is asynchronous"
      Expect.isFalse (Future.isSync f) "Value is not synchronous"
      Expect.equal (Future.get f) 1 "The retrived value is the same"
    }
  ]

let collectionTests =
  testList "Collection Tests" [
    test "Sequential Collection resolves in order of execution" {
      let mutable flag = "none"
      let a = async {
        do! Async.Sleep 1000
        flag <- "a"
        return 2
      }
      let b = async {
        flag <- "b"
        return 4
      }
      let list = [Future.wrap 1; Future.ofAsync a; Future.wrap 3; Future.ofAsync b]
      let res = list |> Future.collectSequential |> Future.map(List.map(Try.get))
      Expect.equal (res |> Future.get) [1;2;3;4] "Values are in order"
      Expect.equal flag "b" "Values were resolved in order"
    }

    test "Parallel Collection resolves in no order" {
      let mutable flag = "none"
      let a = async {
        do! Async.Sleep 1000
        flag <- "a"
        return 2
      }
      let b = async {
        flag <- "b"
        return 4
      }
      let list = [Future.wrap 1; Future.ofAsync a; Future.wrap 3; Future.ofAsync b]
      let res = list |> Future.collectParallel |> Future.map(List.map(Try.get))
      Expect.equal (res |> Future.get) [1;2;3;4] "Values are in order"
      Expect.equal flag "a" "Values were resolved out of order"
    }

    test "Sequential Collection handles errors" {
      let a = async {
        do! Async.Sleep 1000
        return 2
      }
      let error = Failure "Oh No!"
      let list = [Future.wrap 1; Future.ofAsync a; Future.wrap 3; Future.failed(error)]
      let res = list |> Future.collectSequential 
      Expect.equal (res |> Future.get) [Success 1; Success 2; Success 3; FailedWith error] "Error was handled"
    }

    test "Parallel Collection handles errors" {
      let a = async {
        do! Async.Sleep 1000
        return 2
      }
      let error = Failure "Oh No!"
      let list = [Future.wrap 1; Future.ofAsync a; Future.wrap 3; Future.failed(error)]
      let res = list |> Future.collectParallel 
      Expect.equal (res |> Future.get) [Success 1; Success 2; Success 3; FailedWith error] "Error was handled"
    }
  ]

let allTests =
  testList "All Tests" [
    basicTests
    monadTests
    computationTests
    collectionTests
  ]

[<EntryPoint>]
let main args =
  runTests defaultConfig allTests