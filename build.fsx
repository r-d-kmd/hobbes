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

Target.create "PreBuildGenericImages" ignore
Target.create "BuildGenericImages" ignore
Target.create "PostBuildGenericImages" ignore

Target.create "PushGenericImages" ignore

Target.create "PreBuildCommon" ignore
Target.create "PostBuildCommon" ignore

Target.create "PreBuildServiceImages" ignore
Target.create "PostBuildServiceImages" ignore

Target.create "Build" ignore
Target.create "PushDocker" ignore

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

open Fake.Core.TargetOperators
open System.IO

let projectFolder = DirectoryInfo(".")

let changedCommonFiles =
    let serviceDir = 
        DirectoryInfo("./services").FullName
    let collectorDir = 
        DirectoryInfo("./services/collectors").FullName 
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
        elif file.Contains "services" then
            let info = FileInfo file 
            let projectName = 
                if info.Directory.FullName.StartsWith serviceDir then
                    info.DirectoryName
                        .Substring(serviceDir.Length)
                    |> Some
                elif info.Directory.FullName.Contains collectorDir then
                    info.DirectoryName
                        .Substring(collectorDir.Length)
                    |> Some
                else
                    None
                |> Option.bind(fun p -> 
                        p.Split([|'/';'\\'|], System.StringSplitOptions.RemoveEmptyEntries)
                        |> Array.head
                        |> Some
                )
            match projectName with
            None -> l
            | Some projectName ->
                (file, projectName)::l
        else
            l
    ) []

let changeSet = 
    changedCommonFiles 
    |> Seq.map snd 
    |> Set.ofSeq

let hasChanged fileName = 
    changeSet |> Set.contains fileName

let hasDockerChanged = 
    hasChanged "docker"

let hasDependenciesChanged =
    hasDockerChanged || hasChanged "paket.dependencies" || hasChanged "Shared.fs"

let hasCommonChanged = 
    hasChanged "common"

System.IO.Directory.EnumerateFiles("./services","*.fsproj",SearchOption.AllDirectories)
|> Seq.iter(fun projectFilePath ->
    let workingDir = 
        let file = FileInfo(projectFilePath)
        file.Directory.Parent.FullName
    
    let serviceName = Path.GetFileNameWithoutExtension projectFilePath

    let build (tag : string) = 
        let tag = tag.ToLower()
        let tags =
           let t = createDockerTag dockerOrg tag
           [
               t + ":" + assemblyVersion
               t + ":" + "latest"
           ]

        sprintf "build -f %s/docker/Dockerfile.service --build-arg SERVICE_NAME=%s -t %s ." projectFolder.FullName serviceName (tag.ToLower()) 
        |> run "docker" workingDir
        tags
        |> List.iter(fun t -> 
            sprintf "tag %s %s" tag t
            |> run "docker" workingDir
        )

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
            run "docker" workingDir args
        )
    
    let tag = serviceName.ToLower()
    
    let buildTargetName = "Build" + tag 
    let pushTargetName = "Push" + tag 
    Target.create buildTargetName (fun _ -> build tag) 
    Target.create pushTargetName (fun _ -> push tag) 
    "PreBuildServiceImages"
        ==> buildTargetName 
        ==> "PostBuildServiceImages" 
        |> ignore
    buildTargetName 
        ==> pushTargetName 
        |> ignore
) 

let buildImage context path (tag : string) =
    if File.exists path then
        let tag = dockerOrg + "/" + tag.ToLower()
        
        sprintf "build -f %s -t %s ." path tag
        |> run "docker" context

let pushImage path (tag : string) =
    if File.exists path then
        let tag = dockerOrg + "/" + tag.ToLower()
        sprintf "push %s" tag
        |> run "docker" "."

//Generic images
[
    "couchdb"
    "aspnet"
    "sdk"
] |> List.iter(fun name ->
    let buildTargetName = "BuildGeneric" + name
    let pushTargetName = "PushGeneric" + name

    Target.create buildTargetName (fun _ ->  buildImage "." (sprintf "./docker/Dockerfile.%s" name) name)
    "PreBuildGenericImages"
        ==> buildTargetName 
        ==> "PostBuildGenericImages"
        |> ignore
    
    Target.create pushTargetName (fun _ ->  pushImage "." name)
    "PostBuildGenericImages" 
        ==> pushTargetName  
        ==> "PushGenericImages" 
        |> ignore
)

Target.create "BuildSdk" (fun _ ->   
    if hasDependenciesChanged then   
        buildImage "." "./docker/Dockerfile.sdk-hobbes" "sdk:hobbes"
    if hasDockerChanged || hasCommonChanged then
        buildImage "." "./docker/Dockerfile.sdk-service" "sdk:service"
)

Target.create "PushSdk" (fun _ ->  
    if hasDependenciesChanged then   
        pushImage "." "sdk:hobbes"
    if hasDockerChanged || hasCommonChanged then
        pushImage "." "sdk:service"
)

//Set to 'Normal' to have more information when trouble shooting 
let verbosity = Quiet
    
let package conf outputDir projectFile =
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
    [
        "hobbes.core"
        "hobbes.helpers"
        "hobbes.web"
    ] |> List.map(fun n ->
        sprintf "./common/%s/src/%s.fsproj" n n
    )


Target.create "BuildCommon" (fun _ ->
    let commonPack = package DotNet.BuildConfiguration.Release commonLibDir
    commonProjectFiles
    |> Seq.iter commonPack
)

Target.create "DebugCommon" (fun _ ->
    let commonPack = package DotNet.BuildConfiguration.Release commonLibDir
    commonProjectFiles
    |> Seq.iter commonPack
)

let tools = 
    [
        """workbench/src/hobbes.workbench.fsproj"""
    ]

tools
|> List.iter(fun (projectFile) ->     
    let targetName = 
        Path.GetFileNameWithoutExtension(projectFile)
           .Split([|'/';'\\'|],System.StringSplitOptions.RemoveEmptyEntries)
        |> Array.last
    Target.create targetName (fun _ -> package DotNet.BuildConfiguration.Release commonLibDir projectFile)
    "PostBuildCommon"
       ==> targetName
       |> ignore
 )

Target.create "Publish" (fun _ -> 
    run "dotnet" "./workbench/src" "run -- --publish" 
)


"BuildCommon"
    ==> "PostBuildCommon"
    ==> "Build"

"DebugCommon" ?=> "PostBuildCommon"

"PreBuildGenericImages"
    ==> "PostBuildGenericImages"
    ==> "BuildGenericImages" //pseudo target to make it possible to tie build sdk images to the build of generic images
    ==> "PushGenericImages"

"PostBuildCommon"
    ?=> "PreBuildServiceImages"

"BuildSdk"
    ?=> "PreBuildServiceImages"

"PostBuildGenericImages" 
    ?=> "BuildSdk"

"BuildSdk"
    ==> "BuildGenericImages"

"BuildSdk" 
    ==> "PushSdk"
    ==> "PushGenericImages"

(match changedCommonFiles with
 [] -> 
    printfn "No common files changed"
    "PreBuildServiceImages"
 | _ ->
    "PushGenericImages"
) ==> "PostBuildServiceImages"
  ==> "Build"

Target.runOrDefaultWithArguments "Build"