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


let force = (Environment.environVarOrDefault "force" "false").ToLower() = "true"
if force then
    printfn "Running full dependency chain since 'force' was specified"

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
let buildConfigurationName = (Environment.environVarOrDefault "CONFIGURATION" "Debug").ToLower()
let buildConfiguration = 
    match buildConfigurationName with
    "release" -> 
        DotNet.BuildConfiguration.Release
    | _ -> DotNet.BuildConfiguration.Debug
let commonLibDir = "./docker/.lib/"

let assemblyVersion = Environment.environVarOrDefault "APPVEYOR_BUILD_VERSION" "2.0.default"

let createDockerTag dockerOrg (tag : string) = sprintf "%s/hobbes-%s" dockerOrg (tag.ToLower())

open Fake.Core.TargetOperators
open System.IO

let isBelow info (dir: DirectoryInfo) = 
    let rec inner (info : DirectoryInfo) = 
        if info.Parent |> isNull then false
        elif info.FullName = dir.FullName then true
        else
           inner info.Parent
    inner info

[<RequireQualifiedAccess>]
type CommonLib = 
    Core
    | Helpers
    | Web
    | Messaging
    | Any
    with override x.ToString() = 
          match x with
          Core -> "core"
          | Helpers -> "helpers"
          | Web -> "web"
          | Messaging -> "messaging"
          | Any -> "core|helpers|web"

type App = 
    Worker of name:string 
    | Service of name:string
    with member app.NameAndType 
           with get() = 
                match app with
                Worker name ->
                    name, "worker"
                | Service name ->
                    name, "service"
[<RequireQualifiedAccess>]
type DockerStage =
     AppSdk
     | Other

type Change =
   App of App
   | PaketDependencies
   | Docker of DockerStage
   | Common of CommonLib
   | File of string

let dockerDir = DirectoryInfo "./docker"
let serviceDir = DirectoryInfo "./services"
let workerDir = DirectoryInfo "./workers"

let changes =
    let commonDir = DirectoryInfo "./common"
    let coreDir = DirectoryInfo "./common/hobbes.core"
    let helpersDir = DirectoryInfo "./common/hobbes.helpers"
    let webDir = DirectoryInfo "./common/hobbes.web"
    let messagingDir = DirectoryInfo "./common/hobbes.messaging"
    
    Fake.Tools.Git.FileStatus.getChangedFilesInWorkingCopy "." "HEAD@{1}"
    |> Seq.map(fun (_,(file : string)) ->
        let info = FileInfo file
        match info.Name with
        "paket.dependencies" | "paket.references" -> PaketDependencies
        | _ ->
            let dir = info.Directory
            let isBelow = isBelow dir
            let getName (p : string) =
                p.Split([|'/';'\\'|], System.StringSplitOptions.RemoveEmptyEntries)
                |> Array.head 
            if isBelow dockerDir then
                let dockerStage = 
                    match file.Replace("Dockerfile.","") with
                    | "sdk-app" | "hobbes.properties.targets" -> DockerStage.AppSdk
                    | _ -> DockerStage.Other
                Docker dockerStage
            elif isBelow commonDir then
                let cm = 
                    if isBelow coreDir then
                        CommonLib.Core
                    elif isBelow helpersDir then
                        CommonLib.Helpers
                    elif isBelow webDir then
                        CommonLib.Web
                    elif isBelow messagingDir then
                        CommonLib.Messaging
                    else
                        CommonLib.Any //failwithf "Common not known %s" file
                Common cm
            elif isBelow serviceDir then
                info.DirectoryName
                    .Substring(serviceDir.FullName.Length)
                |> getName
                |> Service
                |> App
            elif isBelow workerDir then
                info.DirectoryName
                    .Substring(workerDir.FullName.Length)
                |> getName
                |> Worker
                |> App
            else
                File file
    ) |> Seq.distinct

let commonChanges = 
    changes
    |> Seq.fold(fun lst c ->
        match c with
        Common com -> com::lst 
        | _ -> lst
    ) []

let hasCommonChanged change =
    match change with
    CommonLib.Any -> commonChanges |> List.isEmpty |> not
    | c -> commonChanges |> List.tryFind(fun c' -> c = c') |> Option.isSome

let hasChanged change =
    true 
    //force || changes |> Seq.tryFind (fun c -> c = change) |> Option.isSome
     
let rec shouldRebuildCommon = 
    function
       CommonLib.Web ->
           shouldRebuildCommon CommonLib.Helpers 
           || (CommonLib.Web |> hasCommonChanged)
       | CommonLib.Messaging -> 
           shouldRebuildCommon CommonLib.Web 
           || (CommonLib.Messaging |> hasCommonChanged)
       | common ->
            common
            |> hasCommonChanged

let skipSdkBuilds =
    (Environment.environVarOrDefault "BUILD_ENV" "local") = "AppVeyor"

let hasDockerStageChanged ds = 
    (not skipSdkBuilds) &&  
    (Docker ds |> hasChanged)

let shouldRebuildDependencies = 
    (not skipSdkBuilds) &&  
    hasChanged PaketDependencies

let shouldRebuildAppSdk =
    (not skipSdkBuilds) &&
     (hasDockerStageChanged DockerStage.AppSdk
      || ((not skipSdkBuilds) && (hasChanged (Common CommonLib.Any))
         )
     )

let shouldRebuildService name = 
    shouldRebuildCommon CommonLib.Any
    || shouldRebuildAppSdk 
    || hasChanged (name |> Service |> App)

let shouldRebuildWorker name = 
    shouldRebuildCommon CommonLib.Any
    || shouldRebuildAppSdk 
    || hasChanged (name |> Worker |> App)

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

printfn "Changes: %s" (System.String.Join("\n",changes |> Seq.map (sprintf "%A")))

let commonPath name = 
    sprintf "./common/hobbes.%s/src/hobbes.%s.fsproj" name name
let commons = 
    [
        CommonLib.Web
        CommonLib.Helpers
        CommonLib.Core
        CommonLib.Messaging
    ]

let services = 
    serviceDir.EnumerateFiles("*.fsproj",SearchOption.AllDirectories)
    |> Seq.map(fun file ->
        let workingDir = 
            file.Directory.Parent.FullName
        
        let n = Path.GetFileNameWithoutExtension file.Name
        if n.StartsWith "hobbes." then n.Remove(0,"hobbes.".Length) else n
        , workingDir
    )

let workers = 
    workerDir.EnumerateFiles("*.fsproj",SearchOption.AllDirectories)
    |> Seq.map(fun file ->
        let workingDir = 
            file.Directory.Parent.FullName
        
        Path.GetFileNameWithoutExtension (file.Name.Split('.') |> Array.head), workingDir
    )

let buildImage path (tag : string) =
    let tag = dockerOrg + "/" + tag.ToLower()
    
    sprintf "build -f %s -t %s ." path tag
    |> run "docker" dockerDir.Name

let pushImage (tag : string) =
    let tag = dockerOrg + "/" + tag.ToLower()
    sprintf "push %s" tag
    |> run "docker" dockerDir.Name

let appTargets : seq<_> -> _ = 
    Seq.map(fun (app : App) ->
        let name,_ = app.NameAndType         
        name,(name.ToLower())
    ) >> Map.ofSeq

let serviceTargets = 
    services
    |> Seq.map(fun (serviceName,_) ->
        Service serviceName
    ) |> appTargets

let workerTargets = 
    workers
    |> Seq.map(fun (workerName,_) ->
        Worker workerName
    ) |> appTargets

let setupServiceTarget serviceName = 
    let shouldRebuild = shouldRebuildService serviceName
    let target = serviceTargets.[serviceName]
    target ==> "BuildServices" |> ignore

let setupWorkerTarget workerName = 
    let shouldRebuild = shouldRebuildWorker workerName
    let target = workerTargets.[workerName]
    target ==> "BuildWorkers" |> ignore

let buildApp (app : App) workingDir =
    let name,appType = app.NameAndType
    let tag = name.ToLower()
    

    let build _ = 
        let buildArg = sprintf "%s_NAME=%s" (appType.ToUpper()) name
        let tags =
           let t = createDockerTag dockerOrg tag
           [
               t + ":" + assemblyVersion
               t + ":" + "latest"
           ]

        sprintf "build -f %s/Dockerfile.%s --build-arg %s -t %s ." dockerDir.FullName appType buildArg (tag.ToLower()) 
        |> run "docker" workingDir
        tags
        |> List.iter(fun t -> 
            sprintf "tag %s %s" tag t
            |> run "docker" workingDir
        )

    let push _ = 
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
    
    let buildTargetName = tag 
    let pushTargetName = "Push" + tag 
    
    Target.create buildTargetName build
    Target.create pushTargetName push
    buildTargetName ==> pushTargetName |> ignore

Target.create "All" ignore
Target.create "Build" ignore
Target.create "BuildCommon" ignore

Target.create "BuildServices" ignore
Target.create "BuildWorkers" ignore

Target.create "CleanCommon" (fun _ ->
    let deleteFiles lib =
        [
            sprintf "docker/.lib/hobbes.%s.dll"
            sprintf "docker/.lib/hobbes.%s.deps.json"
            sprintf "docker/.lib/hobbes.%s.pdb"
        ] |> List.iter(fun f -> 
            let file = f lib
            if File.exists file then
                File.delete file
        )
        
    commons
    |> List.filter shouldRebuildCommon
    |> List.iter (string >> deleteFiles)
)

Target.create "Dependencies" (fun _ ->
  let outputDir = "./docker/build"
  let paketDir = outputDir + "/.paket"
  Shell.cleanDirs 
      [
          outputDir
          paketDir
      ]
  run "paket" "." "update"
  [
      "paket.dependencies"
      "paket.lock"
  ] |> Shell.copy outputDir
  System.IO.Directory.EnumerateFiles ".paket"
  |> Shell.copy paketDir
)

services
|> Seq.iter(fun (name,dir) ->
    buildApp (Service name) dir
) 
workers
|> Seq.iter(fun (name,dir) ->
    buildApp (Worker name) dir
) 

let commonTargetName common =
    common.ToString() 
    |> sprintf "BuildCommon%s"

commons |> List.iter(fun common ->
    let commonName = common.ToString()
    let targetName = commonTargetName common
    Target.create targetName (fun _ ->
        let projectFile = commonPath commonName
        package buildConfiguration commonLibDir projectFile
    )
) 



Target.create "Sdk" (fun _ ->   
    let tag = dockerOrg + "/sdk:app"
    let file = "Dockerfile.sdk-app"
    //we do it this way because we want a debug version locally but want to push a release version to docker hub
    sprintf "build -f %s -t %s ." file tag
    |> run "docker" dockerDir.Name
    pushImage "sdk:app"

    //build the debug version for local use
    sprintf "build -f %s -t %s --build-arg CONFIGURATION=%s ." file tag buildConfigurationName
    |> run "docker" dockerDir.Name  
)

services
|> Seq.iter(fun (serviceName,_) ->
        setupServiceTarget serviceName
)

workers
|> Seq.iter(fun (workerName,_) ->
        setupWorkerTarget workerName
)

"CleanCommon" 
    ==> "Dependencies"
    ==> "BuildCommonHelpers" 
    ==> "BuildCommonWeb"
    ==> "BuildCommonMessaging"
    ==> "BuildCommon" 
    ==> "Sdk"

"BuildServices" ==> "Build"
"BuildWorkers" ==> "Build"

"Sdk" 
    ?=> "Build"
    ==> "All"
"Sdk" ==> "All"

Target.runOrDefaultWithArguments "Build"