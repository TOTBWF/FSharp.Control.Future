#r @"packages/build/FAKE/tools/FakeLib.dll"

open System
open Fake
open Fake.Testing

let project = "Future.fs"
let summary = "A small library for better async handling"
let description = "Async handling with immediate values and failure states"
let authors = [ "Reed Mullanix";"Bazinga Technologies Inc" ]
let copyright = "Copyright (c) 2017 Bazinga Technologies Inc"
let tags = "FSharp Async Future"
let solutionFile = "Future.sln"
let testExecutables = "test/bin/Release/**/*Tests*.exe"

Target "Clean" (fun _ -> CleanDir "bin")

Target "CopyBinaries" (fun _ ->
    !! "src/*.??proj"
    |> Seq.map (fun f -> (System.IO.Path.GetDirectoryName f) </> "bin/Release", "bin" </> (System.IO.Path.GetFileNameWithoutExtension f))
    |> Seq.iter(fun (fromDir, toDir) -> CopyDir toDir fromDir (fun _ -> true))
)

Target "Build" (fun _ ->
    !! solutionFile
    |> MSBuildRelease "" "Rebuild"
    |> ignore)

Target "RunTests" (fun _ ->
    !! testExecutables
    |> Expecto.Expecto id
)

Target "All" DoNothing

"Clean"
    ==> "Build"
    ==> "CopyBinaries"
    ==> "RunTests"
    ==> "All"

RunTargetOrDefault "All"