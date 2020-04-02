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
let deployDir = Path.getFullName "../deploy"

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

Target.create "Build"  ignore

Target.create "Bundle" (fun _ ->
    let serverDir = Path.combine deployDir "Server"

    let publishArgs = sprintf "publish -c Release -o \"%s\"" serverDir
    runDotNet publishArgs serverPath ""
)


Target.create "Debug" (fun _ ->
    run "fake" "../../" "build -t DebugCommon"
    let serverDir = Path.combine deployDir "Server"

    let publishArgs = sprintf "publish -c Debug -o \"%s\"" serverDir
    runDotNet publishArgs serverPath ""
)


let buildImage dockerfile _ = 
    let workingDir = "../"
    let arguments = 
        (workingDir
        |> Path.getFullName
        |> Path.getDirectory).ToLower().Split([|'/'; '\\'|], StringSplitOptions.RemoveEmptyEntries)
        |> Array.last
        |> (
            match dockerfile with
            None -> 
                sprintf "build -t %s ."
            | Some dockerfile -> 
                sprintf "build -f %s -t %s ." dockerfile  
        )
        |> String.split ' '
        |> Arguments.OfArgs
    RawCommand ("docker", arguments)
        |> CreateProcess.fromCommand
        |> CreateProcess.withWorkingDirectory workingDir
        |> CreateProcess.ensureExitCode
        |> Proc.run
        |> ignore

Target.create "Restart" (fun _ ->
    
    buildImage (Some "Dockerfile.debug") ()
    
    let compose = run "docker-compose" "../../"
    compose "kill gitgitcollector"
    compose "rm -f gitcollector"
    compose "up gitcollector"
)

Target.create "Redeploy" (fun _ ->
    run "kubectl" "../../../kubernetes" "scale --replicas=0 -f gitcollector-deployment.yaml"
    run "kubectl" "../../../kubernetes" "scale --replicas=1 -f gitcollector-deployment.yaml"
    |> ignore
)


Target.create "BuildImage" (buildImage None)

Target.create "ReleaseBuild" ignore

open Fake.Core.TargetOperators

"ReleaseBuild"
    ==> "Redeploy"

"Clean" 
    ==> "Bundle" 
    ==> "BuildImage"  
    ==> "ReleaseBuild"

"Bundle" ==> "Build"

Target.runOrDefaultWithArguments "ReleaseBuild"