open System
#r "paket: groupref build //"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open Fake.Core
open Fake.IO
open Fake.DotNet
open Fake
open Fake.Tools.Git

let run command workingDir args = 
    let arguments = 
        match args |> String.split ' ' with
        [""] -> Arguments.Empty
        | args -> args |> Arguments.OfArgs
    RawCommand (command, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let serverPath = Path.getFullName "./"
let deployDir = Path.getFullName "./deploy"

Target.create "Clean" (fun _ ->
    [ deployDir ]
    |> Shell.cleanDirs
)

let runDotNet cmd workingDir args =
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd args
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir    

let build configuration workingDir =
    let args = sprintf "--output ../bin/%s --configuration %s" configuration configuration
    runDotNet "build" workingDir args

Target.create "Build" (fun _ ->
    let version = 
        match BuildServer.buildServer with
        BuildServer.AppVeyor -> Environment.environVarOrDefault "APPVEYOR_BUILD_VERSION" "1.0.default"
        | _ -> "1.0.local"
    let gitHash = Information.getCurrentHash()
    AssemblyInfoFile.createFSharp "./AssemblyInfo.fs" [
        AssemblyInfo.Version version
        AssemblyInfo.Title "Hobbes server"
        AssemblyInfo.Metadata("hash", gitHash)
        AssemblyInfo.FileVersion version
        AssemblyInfo.Product "Hobbes server"
    ]
    build "Debug" serverPath
)

Target.create "Bundle" (fun _ ->
    let serverDir = Path.combine deployDir "Server"

    let publishArgs = sprintf "publish -c Release -o \"%s\"" serverDir
    runDotNet publishArgs serverPath ""
)

Target.create "Restart"(fun _ ->
    let compose = run "docker-compose" "."
    compose "kill hobbes"
    compose "rm -f hobbes"
    compose "up hobbes"
)

Target.create "BuildImage" (fun _ ->
    if IO.Directory.Exists("./deploy/Server") |> not then failwith "Doh"
    if IO.Directory.Exists("./deploy/Server/db") |> not then failwith "with What??"
    let workingDir = "../"
    let arguments = 
        (workingDir
        |> Path.getFullName
        |> Path.getDirectory).Split([|'/'; '\\'|], StringSplitOptions.RemoveEmptyEntries)
        |> Array.last
        |> sprintf "build -t kmdrd/hobbes-%s --platform linux ." 
        |> String.split ' '
        |> Arguments.OfArgs
    RawCommand ("docker", arguments)
        |> CreateProcess.fromCommand
        |> CreateProcess.withWorkingDirectory workingDir
        |> CreateProcess.ensureExitCode
        |> Proc.run
        |> ignore
)

open Fake.Core.TargetOperators
"Clean" 
    ==> "Bundle" 
    ==> "Build"
    ==> "BuildImage"
    ==> "Restart"

Target.runOrDefaultWithArguments "BuildImage"