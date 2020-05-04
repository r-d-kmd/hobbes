#r "paket:
nuget Fake ~> 5 //
nuget Fake.Core ~> 5 //
nuget Fake.Core.Target  //
nuget Fake.DotNet //
nuget Fake.DotNet.AssemblyInfoFile //
nuget Fake.DotNet.Cli //
nuget Fake.DotNet.NuGet //
nuget Fake.IO.FileSystem //
nuget Fake.Tools.Git ~> 5 //"
#load "./.fake/build.fsx/intellisense.fsx"


#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators

let dockerOrg = "kmdrd"
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


let dockerFiles = 
    System.IO.Directory.EnumerateFiles("./","Dockerfile",System.IO.SearchOption.AllDirectories)
    |> Seq.filter(fun file ->
        let dockerFolder = 
            "./docker"
            |> System.IO.Path.GetFullPath
            |> System.IO.Path.GetDirectoryName
        let fileFolder =
            file
            |> System.IO.Path.GetFullPath
            |> System.IO.Path.GetDirectoryName
        fileFolder <> dockerFolder
    )

let build configuration workingDir =
    let args = sprintf "--output ./bin/%s --configuration %s" configuration configuration

    DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) "tool restore" |> ignore
    DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) "paket restore" |> ignore

    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) "build" args 
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" "build" workingDir

let fake workdir = 
    run "fake" workdir "build"

let deploy workdir =
    run "fake" workdir "build --target Redeploy"

Target.create "RedeployServer" (fun _ ->
    deploy "./hobbes.server/src"
)

Target.create "RedeployAzure" (fun _ ->
    deploy "./collectors/collectors.azuredevops/src"
)

Target.create "Redeploy" ignore

Target.create "BuildServer" (fun _ ->
    fake "./hobbes.server/src" 
)

Target.create "BuildWorkbench" (fun _ ->
    build "Release" "./workbench/src"
)

Target.create "BuildAzureDevOpsCollector" (fun _ ->
    fake "./collectors/collectors.azuredevops/src"
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

let assemblyVersion = Environment.environVarOrDefault "APPVEYOR_BUILD_VERSION" "1.2.default"

let createDockerTag dockerOrg (tag : string) = sprintf "%s/hobbes-%s" dockerOrg (tag.ToLower())

Target.create "BuildDocker" (fun _ -> 
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

let genericDockerFiles =
    ("./docker/couchdb/","Dockerfile", "couchdb")::
    ([
        "aspnet"
        "sdk"
    ] |> List.map(fun name ->
           ".","./docker/Dockerfile." + name, name
        )
      
    )

let baseDockerFiles = 
    [
        ".","./docker/Dockerfile.sdk-hobbes", "sdk:hobbes"
    ]

let buildImages = 
    Seq.iter(fun (context,path,(tag : string)) ->
        if File.exists path then
            let tag = dockerOrg + "/" + tag.ToLower()
            
            sprintf "build -f %s -t %s ." path tag
            |> run "docker" context
    ) 

Target.create "_BuildGenericImages" (fun _ -> 
    genericDockerFiles
    |> buildImages
)

Target.create "BuildSdkImages" (fun _ -> 
    baseDockerFiles
    |> buildImages
)

let pushImages = 
    Seq.iter(fun (_,path,(tag : string)) ->
        if File.exists path then
            let tag = dockerOrg + "/" + tag.ToLower()
            sprintf "push %s" tag
            |> run "docker" "."
    ) 

Target.create "PushSdkImages" (fun _ -> 
    baseDockerFiles 
    |> pushImages
)

Target.create "PushGenericImages" (fun _ -> 
    genericDockerFiles 
    |> pushImages
)

//Set to 'Normal' to have more information when trouble shooting 
let verbosity = 
    Quiet
    

let package conf outputDir projectFile  _ =
    DotNet.publish (fun opts -> 
                        { opts with 
                               OutputPath = Some outputDir
                               Configuration = conf
                               MSBuildParams = 
                                   { opts.MSBuildParams with
                                          Verbosity = Some verbosity
                                   }    
                        }
                   ) projectFile

let commonProjectFiles =
    !!("common/**/*.fsproj")
    --("common/**/tests/*.fsproj")

Target.create "BuildCommon" ignore
Target.create "_BuildCommon" ignore
Target.create "DebugCommon" ignore
Target.create "PreCommon" ignore

open Fake.Core.TargetOperators

let changedCommonFiles = 
    Fake.Tools.Git.FileStatus.getChangedFilesInWorkingCopy "." "HEAD@{1}"
    |> Seq.fold(fun l (_,(file : string)) ->
        if file.Contains "paket.dependencies" then
            (file,"paket.dependencies")::l
        elif file.Contains "Shared.fs" then
            (file,"Shared.fs")::l
        elif file.Contains "docker/" then
            (file,"docker/")::l
        elif file.Contains "common" then
            (file,"common")::l
        else
            l
    ) []

let buildCommon conf =
    let commonPack = package conf commonLibDir
    commonProjectFiles
    |> Seq.fold(fun prev projectFile ->  
        let targetName =
            (System.IO.Path.GetFileNameWithoutExtension projectFile) +
                match conf with
                DotNet.BuildConfiguration.Release -> ""
                | DotNet.BuildConfiguration.Debug -> "Debug"
                | DotNet.BuildConfiguration.Custom n -> n
    
        Target.create targetName (commonPack projectFile )
        prev
            ==> targetName 
    ) "PreCommon"

buildCommon DotNet.BuildConfiguration.Release ==> "_BuildCommon"
buildCommon DotNet.BuildConfiguration.Debug ==> "DebugCommon"

let tools = 
    [
        """workbench/src/hobbes.workbench.fsproj""", package DotNet.BuildConfiguration.Release commonLibDir
    ]

tools
|> List.fold(fun prev (projectFile, pack) ->     
    let targetName = 
        System.IO.Path.GetFileNameWithoutExtension(projectFile)
           .Split([|'/';'\\'|],System.StringSplitOptions.RemoveEmptyEntries)
        |> Array.last
    Target.create targetName (pack projectFile)
    prev
       ==> targetName
 ) "_BuildCommon"

Target.create "Publish" (fun _ -> 
    run "dotnet" "./workbench/src" "run -- --publish" 
)

Target.create "PushToDocker" (fun _ ->
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

"_BuildGenericImages" 
    ?=> "BuildSdkImages"

"BuildSdkImages"
    ==> "PushSdkImages"
    ==> "PushGenericImages"

"_BuildGenericImages" 
    ==> "PushGenericImages"

"RedeployServer"
    ==> "Redeploy"

"RedeployAzure"
    ==> "Redeploy"

(match changedCommonFiles with
 [] -> 
    printfn "No common files changed"
    "PreCommon"
 | _ ->
    printfn "Common files have changed: %A" changedCommonFiles
    "PushSdkImages"
) ==> "BuildDocker"
  ==> "Build"

"_BuildCommon"
    ==> "BuildSdkImages"
    ==> "BuildCommon"

Target.runOrDefaultWithArguments "Build"