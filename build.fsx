open Fake.SystemHelper
#r "paket: groupref build //"
#load "./.fake/build.fsx/intellisense.fsx"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open Fake.Core
open Fake.DotNet
open Fake.DotNet.NuGet
open Fake.IO

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


let dockerFiles = System.IO.Directory.EnumerateFiles("./","Dockerfile",System.IO.SearchOption.AllDirectories)
let projFiles = System.IO.Directory.EnumerateFiles("./","*.fsproj",System.IO.SearchOption.AllDirectories)

let build configuration workingDir =
    let args = sprintf "--output ./bin/%s --configuration %s" configuration configuration

    DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) "tool restore" |> ignore
    DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) "paket restore" |> ignore

    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) "build" args 
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" "build" workingDir

let buildProjects config = 
    projFiles
    |> Seq.iter(fun file ->
        let workDir = System.IO.Path.GetDirectoryName file
        if System.IO.File.Exists(System.IO.Path.Combine(workDir,"build.fsx")) then
            run "fake" workDir "build"
        else
            build config workDir
    )

Target.create "Compile" (fun _ ->
    buildProjects "Debug"
)

Target.create "Build" ignore

Target.create "ReleaseBuild" (fun _ ->
    buildProjects "Release"
)

Target.create "Test" (fun _ ->

    let envIsRunning() = 
        let output = 
            RawCommand ("docker", ["ps"] |> Arguments.OfArgs)
            |> CreateProcess.fromCommand
            |> CreateProcess.redirectOutput
            |> Proc.run
    
        let containers = 
            output.Result.Output.Split([|"\n"|], System.StringSplitOptions.RemoveEmptyEntries)
            |> Seq.map(fun row -> 
                row.Split([|"\t";" "|], System.StringSplitOptions.RemoveEmptyEntries)
                |> Array.last
            ) |> Seq.tail
        
        if containers |> Seq.filter(fun image -> image = "hobbes" || image = "db") |> Seq.length = 2 then
            true
        else
            printfn "Containers currently running %A" (containers |> Seq.map (sprintf "%A"))
            false

    let test = 
        let rec retry count = 
            if count > 0 && (envIsRunning() |> not) then
                async {
                    do! Async.Sleep 5000
                    return! retry (count - 1)
                }
            else
                async {
                    do! Async.Sleep 20000 //the container has started but wait until it's ready
                    printfn "Starting testing"
                    System.IO.Directory.EnumerateFiles("./","*.fsproj",System.IO.SearchOption.AllDirectories)
                    |> Seq.filter(fun f ->
                        f.Contains("tests")
                    ) |> Seq.iter(fun projectFile ->
                        let workingDir = 
                            projectFile
                            |> Path.getFullName
                            |> Path.getDirectory
                        if workingDir.TrimEnd('/','\\').EndsWith("tests") then
                            DotNet.test (DotNet.Options.withWorkingDirectory workingDir) ""
                    )
                }
        retry 24

    let startEnvironment = async {
        let workDir = "./hobbes.server"
        run "docker-compose" workDir "kill"
        run "docker-compose" workDir "up -d"
    }

    let tasks =
        [ 
          startEnvironment
          test
        ]

    tasks
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
    
)

let inline (@@) p1 p2 = 
    System.IO.Path.Combine(p1,p2)

let projectName = "hobbes"
let buildDir = "./hobbes.core/src/bin"
let packagingRoot = "./packaging"
let packagingDir = packagingRoot @@ "hobbes"
let netDir = packagingDir @@ "lib/net45" |> System.IO.Path.GetFullPath

        
let CleanDirs dirs = 
    dirs
    |> List.map System.IO.Path.GetFullPath
    |> List.iter(fun dir ->
        if System.IO.Directory.Exists dir then
            System.IO.Directory.Delete(dir, true)
        printfn "Creating directory %s" dir
        System.IO.Directory.CreateDirectory(dir) |> ignore
    )   

Target.create "Clean" (fun _ ->
    CleanDirs [
        buildDir
        packagingRoot
        packagingDir 
        netDir
    ]
)

let assemblyVersion = Environment.environVarOrDefault "APPVEYOR_BUILD_VERSION" "1.2.default"

let createDockerTag dockerOrg tag = sprintf "%s/hobbes-%s" dockerOrg tag

Target.create "BuildDocker" (fun _ -> 
    let dockerOrg = "kmdrd"
    let run = run "docker"
     
    printfn "Found docker files: (%A)" dockerFiles
    dockerFiles
    |> Seq.iter(fun path ->
        let workingDir = System.IO.Path.GetDirectoryName path
        
        let build (tag : string) = 
            let tag = tag.ToLower()
            let tags =
               let t = createDockerTag dockerOrg tag
               [
                   t + ":" + assemblyVersion
                   t + ":" + "latest"
               ]

            sprintf "build -t %s --platform linux ." tag
            |> run workingDir
            tags
            |> List.iter(fun t -> 
                sprintf "tag %s %s" tag t
                |> run workingDir
            )

        let tag = (workingDir.Split([|'/';'\\'|],System.StringSplitOptions.RemoveEmptyEntries) |> Array.last).ToLower()
        build tag
    ) 
)

Target.create "Publish" (fun _ -> 
    run "dotnet" "./workbench/src" "run -- --publish" 
)

Target.create "PushToDocker" (fun _ ->
    let dockerOrg = "kmdrd"
    let run = run "docker"
    
    dockerFiles
    |> Seq.iter(fun path ->
        let path = System.IO.Path.GetFullPath path
        let workingDir = System.IO.Path.GetDirectoryName path
        
        let push (tag : string) = 
            let tags =
               let t = createDockerTag dockerOrg (tag.ToLower())
               [
                   t + ":" + assemblyVersion
                   t + ":" + "latest"
               ]
            tags
            |> List.iter(fun tag ->
                let args = sprintf "push %s" <| tag
                printfn "Executing: $ docker %s" args
                run workingDir args
            )
        let tag = workingDir.Split([|'/';'\\'|],System.StringSplitOptions.RemoveEmptyEntries) |> Array.last
      
        push tag
    ) 
)

Target.create "PushAlpha" (fun _ ->
    let dockerOrg = "kmdrd"
    let run = run "docker"
    dockerFiles
    |> Seq.iter(fun path ->
        let path = System.IO.Path.GetFullPath path
        let workingDir = System.IO.Path.GetDirectoryName path
   
        let push (tag : string) = 
            let t = createDockerTag dockerOrg (tag.ToLower()) + ":" + "alpha"
            sprintf "tag %s %s" tag t
             |> run workingDir 
            
            let args = sprintf "push %s" <| t
            printfn "Executing: $ docker %s" args
            run workingDir args
            
        let tag = workingDir.Split([|'/';'\\'|],System.StringSplitOptions.RemoveEmptyEntries) |> Array.last
      

        push tag
    ) 
)

open Fake.Core.TargetOperators

"Clean"
   ==> "Compile"
   ==> "BuildDocker"
   ==> "PushAlpha"
   ==> "Build"

"Clean"
   ==> "ReleaseBuild"
   ==> "BuildDocker"
   ==> "PushToDocker"

Target.runOrDefaultWithArguments "Build"