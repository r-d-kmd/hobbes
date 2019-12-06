open System
#r "paket: 
nuget Fake
nuget Fake.Core
nuget Fake.Core.Target
nuget Fake.DotNet
nuget Fake.DotNet.NuGet
nuget Fake.IO.FileSystem
nuget Fake.DotNet.Cli //"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open Fake.Core
open Fake.IO
open Fake.DotNet

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
    build "Debug" serverPath
)

Target.create "Bundle" (fun _ ->
    let serverDir = Path.combine deployDir "Server"

    let publishArgs = sprintf "publish -c Release -o \"%s\"" serverDir
    runDotNet publishArgs serverPath ""
)

Target.create "BuildImage" (fun _ ->
    let workingDir = "../"
    let arguments = 
        ((workingDir
        |> Path.getFullName
        |> Path.getDirectory).Split([|'/'; '\\'|], StringSplitOptions.RemoveEmptyEntries)
        |> Array.last).ToLower()
        |> sprintf "build -t kmdrd/hobbes-%s ." 
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

Target.runOrDefaultWithArguments "BuildImage"