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

let build configuration workingDir =
    let args = sprintf "--output ./bin/%s --configuration %s" configuration configuration

    DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) "tool restore" |> ignore
    DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) "paket restore" |> ignore

    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) "build" args 
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" "build" workingDir

let fake workdir = 
    run "fake" workdir "build"

Target.create "BuildServer" (fun _ ->
    fake "./hobbes.server/src" 
)

Target.create "BuildWorkbench" (fun _ ->
    build "Release" "./workbench/src"
)

Target.create "BuildAzureDevOpsCollector" (fun _ ->
    fake "./collectors/collectors.AzureDevOps/src"
)

Target.create "Build" ignore

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

let commonLibDir = "./.lib/"
        
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
        commonLibDir
    ]
)

let assemblyVersion = Environment.environVarOrDefault "APPVEYOR_BUILD_VERSION" "1.2.default"

let createDockerTag dockerOrg (tag : string) = sprintf "%s/hobbes-%s" dockerOrg (tag.ToLower())

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

            sprintf "build -t %s ." (tag.ToLower())
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

//Set to 'Normal' to have more information when trouble shooting 
let verbosity = 
    Quiet
    

let package projectFile _ =
    DotNet.publish (fun opts -> 
                        { opts with 
                               OutputPath = Some commonLibDir
                               Configuration = DotNet.BuildConfiguration.Release
                               MSBuildParams = 
                                   { opts.MSBuildParams with
                                          Verbosity = Some verbosity
                                   }    
                        }
                   ) projectFile

let commonLibs =
    [
        "core"
        "helpers"
        "web"
    ]

Target.create "BuildCommon" ignore
Target.create "ReleaseBuild" ignore

open Fake.Core.TargetOperators

(commonLibs
 |> List.fold (fun prev name -> 
     let projectName = sprintf "hobbes.%s" name 
     let projectFile = sprintf "./%s/src/%s.fsproj" projectName projectName
     Target.create projectName (package projectFile)
     prev
         ==> projectName 
 ) "Clean")
    ==> "BuildCommon"

let tools = 
    [
        """hobbes.server/src/hobbes.server.fsproj"""
        """collectors/collectors.azuredevops/src/collectors.azuredevops.fsproj"""
        """workbench/src/hobbes.workbench.fsproj"""
    ]

(tools
|> List.fold(fun prev projectFile ->     
    let targetName = 
        System.IO.Path.GetFileNameWithoutExtension(projectFile)
           .Split([|'/';'\\'|],System.StringSplitOptions.RemoveEmptyEntries)
        |> Array.last
    Target.create targetName (package projectFile)
    prev
       ==> targetName
 ) "BuildCommon"
) ==> "ReleaseBuild"

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
                let args = sprintf "push %s" <| (tag.ToLower())
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
            let tag = tag.ToLower()
            let t = createDockerTag dockerOrg tag + ":" + "alpha"
            sprintf "tag %s %s" tag t
             |> run workingDir 
            
            let args = sprintf "push %s" <| t
            printfn "Executing: $ docker %s" args
            run workingDir args
            
        let tag = workingDir.Split([|'/';'\\'|],System.StringSplitOptions.RemoveEmptyEntries) |> Array.last
      

        push tag
    ) 
)


"ReleaseBuild"
    ==> "BuildDocker"
    ==> "Build"

"BuildDocker" 
    ?=>"PushAlpha"
    ==> "Build"

"BuildDocker"
    ==> "PushToDocker"

Target.runOrDefaultWithArguments "Build"