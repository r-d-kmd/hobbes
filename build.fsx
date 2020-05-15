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


let commonLibDir = "./.lib/"

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
type CommonLib = 
    Core
    | Helpers
    | Web
    | Any
    with override x.ToString() = 
          match x with
          Core -> "core"
          | Helpers -> "helpers"
          | Web -> "web"
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

type Change =
   App of App
   | Collector of string
   | PaketDependencies
   | Shared
   | Docker
   | Common of CommonLib
   | File of string

let dockerDir = DirectoryInfo "./docker"
let serviceDir = DirectoryInfo("./services")
let workerDir = DirectoryInfo("./workers")

let changes =
    let coreDir = DirectoryInfo "./common/hobbes.core"
    let helpersDir = DirectoryInfo "./common/hobbes.helpers"
    let webDir = DirectoryInfo "./common/hobbes.web"
    let collectorDir = DirectoryInfo("./services/collectors") 
    Fake.Tools.Git.FileStatus.getChangedFilesInWorkingCopy "." "HEAD@{1}"
    |> Seq.map(fun (_,(file : string)) ->
        let info = FileInfo file
        match info.Name with
        "paket.dependencies" | "paket.references" -> PaketDependencies
        | "Shared.fs" -> Shared
        | _ ->
            let dir = info.Directory
            let isBelow = isBelow dir
            let getName (p : string) =
                p.Split([|'/';'\\'|], System.StringSplitOptions.RemoveEmptyEntries)
                |> Array.head 
            if isBelow dockerDir then
                Docker
            elif isBelow coreDir then
                Common Core
            elif isBelow helpersDir then
                Common Helpers
            elif isBelow webDir then
                Common Web
            elif isBelow collectorDir then
                info.DirectoryName
                    .Substring(collectorDir.FullName.Length)
                |> getName
                |> Collector
            elif isBelow serviceDir then
                info.DirectoryName
                    .Substring(serviceDir.FullName.Length)
                |> getName
                |> Service
                |> App
            elif isBelow workerDir then
                info.DirectoryName
                    .Substring(serviceDir.FullName.Length)
                |> getName
                |> Worker
                |> App
            else
                File file
    ) |> Seq.distinct


let hasChanged change = 
    changes |> Seq.tryFind (function
                              Common c ->
                                  match change with
                                  Common Any -> true
                                  | Common c' when c' = c -> true
                                  | _ -> false
                              | c -> c = change) |> Option.isSome

let shouldRebuildGenericDockerImages = 
    hasChanged Docker

let shouldRebuildHobbesSdk =
    shouldRebuildGenericDockerImages || hasChanged PaketDependencies

let shouldRebuildServiceSdk =
    shouldRebuildHobbesSdk || hasChanged Shared || hasChanged (Common Any)

let shouldRebuildWorkerSdk =
    shouldRebuildHobbesSdk || hasChanged Shared || hasChanged (Common Any)

let shouldRebuildService name = 
    shouldRebuildServiceSdk || hasChanged (name |> Service |> App)

let shouldRebuildWorker name = 
    shouldRebuildWorkerSdk || hasChanged (name |> Worker |> App)

let rec shouldRebuildCommon = 
    function
       Web ->
           //Web depends Helpers
           shouldRebuildCommon Helpers || (Web |> Common |> hasChanged)
       | common ->
            common
            |> Common
            |> hasChanged
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

printfn "Changes: %A" changes

let commonPath name = 
    sprintf "./common/hobbes.%s/src/hobbes.%s.fsproj" name name
let commons = 
    [
        Web
        Helpers
        Core
    ]

let services = 
    serviceDir.EnumerateFiles("*.fsproj",SearchOption.AllDirectories)
    |> Seq.map(fun file ->
        let workingDir = 
            file.Directory.Parent.FullName
        
        Path.GetFileNameWithoutExtension file.Name, workingDir
    )

let workers = 
    workerDir.EnumerateFiles("*.fsproj",SearchOption.AllDirectories)
    |> Seq.map(fun file ->
        let workingDir = 
            file.Directory.Parent.FullName
        
        Path.GetFileNameWithoutExtension (file.Name.Split('.') |> Array.head), workingDir
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

let genricImages = 
    [
        "couchdb"
        "aspnet"
        "sdk"
    ]

let appTargets : seq<_> -> _ = 
    Seq.map(fun (app : App) ->
        let name,appType = app.NameAndType         
        name,"Build" + appType + (name.ToLower())
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
    "PreBuildServices" 
        ==> serviceTargets.[serviceName]
        ==> "BuildServices" 
        |> ignore

let setupWorkerTarget workerName = 
    "PreBuildworkers" 
        ==> workerTargets.[workerName]
        ==> "Buildworkers" 
        |> ignore

let buildApp (app : App) workingDir =
    let name,appType = app.NameAndType
    let tag = name.ToLower()
    

    let build _ = 
        let buildArg = sprintf "%s=%s" (appType.ToUpper()) name
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
    
    let buildTargetName = "Build" + appType + tag 
    let pushTargetName = "Push" + appType + tag 
    Target.create buildTargetName build
    Target.create pushTargetName push
    "PreBuild" + appType + "s" ?=> buildTargetName |> ignore
    buildTargetName ==> pushTargetName |> ignore

Target.create "ForceBuildServices" ignore
Target.create "ForceBuildWorkers" ignore
Target.create "Rebuild" ignore
Target.create "Build" ignore
Target.create "BuildCommon" ignore
Target.create "BuildGenericImages" ignore
Target.create "PushGenericImages" ignore
Target.create "BuildServices" ignore
Target.create "PreBuildServices" ignore
Target.create "BuildWorkers" ignore
Target.create "PreBuildWorkers" ignore



services
|> Seq.iter(fun (name,dir) ->
    buildApp (Service name) dir
) 
workers
|> Seq.iter(fun (name,dir) ->
    buildApp (Worker name) dir
) 

//Generic images
genricImages
|> List.iter(fun name ->
    let buildTargetName = "BuildGeneric" + name
    let pushTargetName = "PushGeneric" + name
    
    Target.create buildTargetName (fun _ ->  buildImage "." (sprintf "./docker/Dockerfile.%s" name) name)
    Target.create pushTargetName (fun _ ->  pushImage "." name)
    buildTargetName  ==> pushTargetName |> ignore
)

let commonTargetName common =
    common.ToString() 
    |> sprintf "BuildCommon%s"
commons |> List.iter(fun common ->
    let commonName = common.ToString()
    let targetName = commonTargetName common
    Target.create targetName (fun _ ->
        let projectFile = commonPath commonName
        package DotNet.BuildConfiguration.Release commonLibDir projectFile
    )
) 

Target.create "PushHobbesSdk" (fun _ ->  
    pushImage "." "sdk:hobbes"
)

Target.create "PushServiceSdk" (fun _ ->  
    pushImage "." "sdk:service"
)

Target.create "BuildServiceSdk" (fun _ ->   
    buildImage "." "./docker/Dockerfile.sdk-service" "sdk:service"
)

Target.create "BuildHobbesSdk" (fun _ ->   
        buildImage "." "./docker/Dockerfile.sdk-hobbes" "sdk:hobbes"
)
Target.create "BuildWorkbench" (fun _ -> 
        package DotNet.BuildConfiguration.Release commonLibDir  """workbench/src/hobbes.workbench.fsproj"""
)
       
Target.create "Publish" (fun _ -> 
    run "dotnet" "./workbench/src" "run -- --publish" 
)

commons |> List.iter(fun common ->
    if shouldRebuildCommon common then 
        let targetName = commonTargetName common
        targetName ==> "BuildCommon" |> ignore
)
commonTargetName Helpers ?=> commonTargetName Web 

services
|> Seq.iter(fun (serviceName,_) ->
    if shouldRebuildService serviceName then
        setupServiceTarget serviceName
)

workers
|> Seq.iter(fun (workerName,_) ->
    if shouldRebuildWorker workerName then
        setupWorkerTarget workerName
)

if shouldRebuildServiceSdk then
    "BuildCommon" ?=> "BuildServiceSdk" |> ignore
    "BuildHobbesSdk" ?=> "BuildServiceSdk" |> ignore
    "BuildServiceSdk" ==> "PushServiceSdk" ==> "Build" |> ignore

if shouldRebuildHobbesSdk then   
    "BuildGenericImages" ?=> "BuildHobbesSdk" |> ignore
    "BuildHobbesSdk" ==> "PushHobbesSdk" ==> "Build" |> ignore

if shouldRebuildGenericDockerImages then
    "BuildGenericImages" ==> "PushGenericImages" ==> "Build" |> ignore

services
|> Seq.iter(fun (serviceName, _) ->
     serviceTargets.[serviceName]
         ==> "ForceBuildServices" |> ignore
)

workers
|> Seq.iter(fun (workerName, _) ->
     workerTargets.[workerName]
         ==> "ForceBuildWorkers" |> ignore
)

"BuildServiceSdk" ?=> "PreBuildServices" 
"BuildServiceSdk" ?=> "PreBuildWorkers" 
"BuildServices" ==> "Build"
"BuildWorkers" ==> "Build"
"BuildCommon" ?=> "BuildWorkbench"
"BuildWorkbench" ==> "Publish"
"ForceBuildServices" ==> "Rebuild"
"ForceBuildWorkers" ==> "Rebuild"

Target.runOrDefaultWithArguments "Build"