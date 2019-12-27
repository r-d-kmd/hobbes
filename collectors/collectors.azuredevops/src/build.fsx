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

let buildImage dockerfile _ = 
    let workingDir = "../"
    let arguments = 
        (workingDir
        |> Path.getFullName
        |> Path.getDirectory).Split([|'/'; '\\'|], StringSplitOptions.RemoveEmptyEntries)
        |> Array.last
        |> (
            match dockerfile with
            None -> 
                fun t -> sprintf "build -t %s ." (t.ToLower())
            | Some dockerfile -> 
                fun t -> sprintf "build -f %s -t %s ." dockerfile  (t.ToLower())
        )
        |> String.split ' '
        |> Arguments.OfArgs
    RawCommand ("docker", arguments)
        |> CreateProcess.fromCommand
        |> CreateProcess.withWorkingDirectory workingDir
        |> CreateProcess.ensureExitCode
        |> Proc.run
        |> ignore

Target.create "Clean" (fun _ ->
    [ deployDir ]
    |> Shell.cleanDirs
)

Target.create "Restart" (fun _ ->
    buildImage (Some "Dockerfile.debug") ()
    let compose = run "docker-compose" "."
    compose "kill azuredevopscollector"
    compose "rm -f azuredevopscollector"
    compose "up azuredevopscollector"
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

Target.create "Debug" (fun _ ->
    let serverDir = Path.combine deployDir "Server"

    let publishArgs = sprintf "publish -c Debug -o \"%s\"" serverDir
    runDotNet publishArgs serverPath ""
)

Target.create "Bundle" (fun _ ->
    let serverDir = Path.combine deployDir "Server"

    let publishArgs = sprintf "publish -c Release -o \"%s\"" serverDir
    runDotNet publishArgs serverPath ""
)

Target.create "Redeploy" (fun _ ->
    run "kubectl" "../../../kubernetes" "scale --replicas=0 -f azuredevops-deployment.yaml"
    run "kubectl" "../../../kubernetes" "scale --replicas=1 -f azuredevops-deployment.yaml"
   |> ignore
)


open Fake.Core.TargetOperators

"Build"
    ==> "Redeploy"

"Clean" 
    ==> "Bundle" 
    ==> "Build"

"Debug"
    ==> "Restart"

Target.runOrDefaultWithArguments "Build"