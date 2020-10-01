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

[<RequireQualifiedAccess>]
type Targets = 
   GenericSdk 
   | CleanCommon
   | Dependencies
   | Core
   | Web
   | Messaging
   | PushCommonNugets
   | Sdk
   | SdkImage
   | PreApps
   | Build
   | All
   | Complete
   | PushApps
   | BuildForTest
   | PullSdk
   | PullApp
   | PullDb
   | PullRuntime
   | TestNoBuild
   | Runtime
   | Generic of string

let targetName = 
    function
        Targets.GenericSdk -> "GenericSdk"
       | Targets.CleanCommon -> "CleanCommon"
       | Targets.Dependencies -> "Dependencies"
       | Targets.Core -> "Core"
       | Targets.Web -> "Web"
       | Targets.Messaging -> "Messaging"
       | Targets.PushCommonNugets -> "PushCommonNugets"
       | Targets.SdkImage -> "SdkImage"
       | Targets.Sdk -> "Sdk"
       | Targets.PreApps -> "PreApps"
       | Targets.Build -> "Build"
       | Targets.All -> "All"
       | Targets.Complete -> "Complete"
       | Targets.PushApps -> "PushApps"
       | Targets.BuildForTest -> "BuildForTest"
       | Targets.Generic name -> name
       | Targets.PullSdk  -> "PullSdk"
       | Targets.PullDb -> "PullDb"
       | Targets.PullRuntime  -> "PullRuntime"
       | Targets.TestNoBuild -> "TestNoBuild"
       | Targets.PullApp -> "PullApp"
       | Targets.Runtime -> "Runtime"



open Fake.Core.TargetOperators
let inline (==>) (lhs : Targets) (rhs : Targets) =
    Targets.Generic((targetName lhs) ==> (targetName rhs))

let inline (?=>) (lhs : Targets) (rhs : Targets) =
    Targets.Generic((targetName lhs) ?=> (targetName rhs))

let create target = 
    target
    |> targetName
    |> Target.create

let runOrDefaultWithArguments =
    targetName
    >> Target.runOrDefaultWithArguments 

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
let nugetFeedUrl = "https://kmddk.pkgs.visualstudio.com/45c29cd0-03bf-4f63-ac71-3c366095dda9/_packaging/KMD_Package_Feed/nuget/v2"
//let buildConfigurationName = (Environment.environVarOrDefault "CONFIGURATION" "RELEASE").ToLower()
let buildConfiguration = 
    //match buildConfigurationName with
    //"release" -> 
        printfn "Using release configuration"
        DotNet.BuildConfiguration.Release
    //| _ -> DotNet.BuildConfiguration.Debug
type DockerCommand = 
    Push of string
    | Pull of string
    | Build of file:string option * tag:string * buildArgs: (string * string) list
    | Tag of original:string * newTag:string

let docker command dir =
    let arguments = 
        match command with
        Push tag -> sprintf "push %s" tag
        | Pull tag -> sprintf "pull %s" tag
        | Build(file,tag,buildArgs) -> 
            let buildArgs = 
                System.String.Join(" ", 
                    buildArgs 
                    |> List.map(fun (n,v) -> sprintf "--build-arg %s=%s" n v)
                ).Trim()
            ( match file with
              None -> 
                  sprintf "build -t %s %s ."  
              | Some f -> sprintf "build -f %s -t %s %s ." f) (tag.ToLower()) buildArgs
        | Tag(t1,t2) -> sprintf "tag %s %s" t1 t2
    let arguments = 
        //replace multiple spaces with just one space
        let args = arguments.Split([|' '|], System.StringSplitOptions.RemoveEmptyEntries)
        System.String.Join(" ",args) 
    run "docker" dir (arguments.Replace("  "," ").Trim())
let commonLibDir = "./docker/.lib/"

let assemblyVersion = Environment.environVarOrDefault "APPVEYOR_BUILD_VERSION" "2.0.default"

let createDockerTag dockerOrg (tag : string) = sprintf "%s/hobbes-%s" dockerOrg (tag.ToLower())


open System.IO

let isBelow info (dir: DirectoryInfo) = 
    let rec inner (info : DirectoryInfo) = 
        if info.Parent |> isNull then false
        elif info.FullName = dir.FullName then true
        else
           inner info.Parent
    inner info

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

let dockerDir = DirectoryInfo "./docker"
let serviceDir = DirectoryInfo "./services"
let workerDir = DirectoryInfo "./workers"

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


let commonPath target =
    let name =
        (target
         |> targetName).ToLower()
    sprintf "./common/hobbes.%s/src/hobbes.%s.fsproj" name name
let commons = 
    [
        Targets.Web
        Targets.Core
        Targets.Messaging
    ]
let apps : seq<App*string> = 
    let enumerateProjectFiles (dir : DirectoryInfo) = 
        dir.EnumerateFiles("*.fsproj",SearchOption.AllDirectories)
        |> Seq.filter(fun n -> n.Name.ToLower().EndsWith(".tests.fsproj") |> not) //exclude test projects
    let services = 
        serviceDir
        |> enumerateProjectFiles
        |> Seq.map(fun file ->
            let workingDir = 
                file.Directory.Parent.FullName
            
            let n = Path.GetFileNameWithoutExtension file.Name
            let name = 
                if n.StartsWith "hobbes." then n.Remove(0,"hobbes.".Length) else n
            Service name, workingDir
        )

    let workers = 
        workerDir
        |> enumerateProjectFiles
        |> Seq.map(fun file ->
            let workingDir = 
                file.Directory.Parent.FullName
            
            Path.GetFileNameWithoutExtension (file.Name.Split('.') |> Array.head) |> Worker, workingDir
        )
    services |> Seq.append workers

let buildApp (name : string) (appType : string) workingDir =
    
    let tag = name.ToLower()
    
    let build _ = 
        
        let tags =
           let t = createDockerTag dockerOrg tag
           [
               t + ":" + assemblyVersion
               t + ":" + "latest"
           ]

        let file = 
            sprintf "%s/Dockerfile.service" dockerDir.FullName
            |> System.IO.File.ReadAllText
        let content = 
            match appType.ToLower() with
            "service" -> 
                file.Replace("${SERVICE_NAME}",name)
            | "worker" ->
                file.Replace("${SERVICE_NAME}",name + ".worker")
            | _ -> failwithf "Don't know app type %s" appType

        let dest = sprintf "%s/Dockerfile" workingDir
        System.IO.File.WriteAllText(dest,content)
        printfn "Dockerfile (%s) written to %s" content dest

        docker (Build(None,tag,[])) workingDir
        tags
        |> List.iter(fun t -> 
            docker (Tag(tag,t)) workingDir
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
            docker (Push tag) workingDir
        )
    
    let buildTarget = Targets.Generic tag 
    let pushTarget = "Push" + tag |> Targets.Generic
    
    create buildTarget build
    create pushTarget push
    buildTarget ==> pushTarget ==> Targets.PushApps |> ignore

create Targets.Complete ignore
create Targets.PushApps ignore
create Targets.All ignore
create Targets.Build ignore
create Targets.Sdk ignore

create Targets.PreApps ignore
create Targets.BuildForTest ignore

create Targets.CleanCommon (fun _ ->
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
    |> List.iter (string >> deleteFiles)
)

create Targets.Dependencies (fun _ ->
  let outputDir = "./docker/build"
  let paketDir = outputDir + "/.paket"
  Shell.cleanDirs 
      [
          outputDir
          paketDir
      ]
  
  [
      "paket.dependencies"
      "paket.lock"
  ] |> Shell.copy outputDir
  System.IO.Directory.EnumerateFiles ".paket"
  |> Shell.copy paketDir
)

apps
|> Seq.iter(fun (app,dir) ->
    let name,appType = app.NameAndType
    buildApp name appType dir
    Targets.PreApps ==> Targets.Generic(name) ==> Targets.Build |> ignore
) 

commons |> List.iter(fun common ->
    let targetName = common
    create targetName (fun _ -> 
        let projectFile = commonPath common
        package buildConfiguration commonLibDir projectFile
        let commonSrcPath = Path.Combine(Path.GetDirectoryName(projectFile),"..")
        let packages = Directory.EnumerateFiles(commonSrcPath, "*.nupkg")
        let dateTime = System.DateTime.UtcNow
        let version = sprintf "1.%i.%i.%i-default" dateTime.Year dateTime.DayOfYear ((int) dateTime.TimeOfDay.TotalSeconds)
        File.deleteAll packages
        sprintf "pack --version %s ." version
        |> run "paket" commonSrcPath 
        let nupkgFilePath = Directory.EnumerateFiles(commonSrcPath, "*.nupkg")
                            |> Seq.exactlyOne
        sprintf "push --url %s --api-key na %s" nugetFeedUrl nupkgFilePath
        |> run "paket" "./"
    )
) 

create Targets.GenericSdk (fun _ ->   
    let tag = sprintf "%s/sdk" dockerOrg
    let build file = 
       docker (Build(Some file,tag,[])) dockerDir.Name

    build "Dockerfile.sdk"
    docker (Push tag) dockerDir.Name
)

create Targets.Runtime (fun _ ->
    let isDebug = buildConfiguration = DotNet.BuildConfiguration.Debug
    let runtimeFileVersion = 
        if isDebug then
           "Dockerfile.runtime-debug"
        else
           "Dockerfile.runtime" 
    let tag = sprintf "%s/runtime"dockerOrg
    docker (Build(Some(runtimeFileVersion),tag,[])) dockerDir.Name
    if not isDebug then
        docker (Push tag) dockerDir.Name
)

create Targets.SdkImage (fun _ ->   
    let tag = sprintf "%s/app" dockerOrg
    let file = Some("Dockerfile.app")
    let build configuration = 
        docker (Build(file,tag,["CONFIGURATION",configuration])) dockerDir.Name
    match buildConfiguration with 
    DotNet.BuildConfiguration.Release ->
        build "Release"
        docker (Push tag) dockerDir.Name
    | _ -> build "Debug"
)

create Targets.TestNoBuild (fun _ ->
    run "setupTest" "." "" |> ignore
)

create Targets.PullRuntime (fun _ ->
    docker (Pull "kmdrd/runtime") "."
)

create Targets.PullSdk (fun _ ->
    docker (Pull "kmdrd/sdk") "."
)

create Targets.PullApp (fun _ ->
    docker (Pull "kmdrd/app") "."
)

create Targets.PullDb (fun _ ->
    docker (Pull "kmdrd/couchdb") "."
)

Targets.Dependencies 
    ?=> Targets.Core 
    ==> Targets.SdkImage

Targets.Dependencies
    ?=> Targets.Web
    ==> Targets.SdkImage

Targets.Dependencies
    ?=> Targets.Messaging
    ==> Targets.SdkImage

Targets.SdkImage
    ?=> Targets.Runtime
    ==> Targets.Sdk

Targets.GenericSdk
    ?=> Targets.CleanCommon
    ==> Targets.Dependencies
    ==> Targets.Sdk
    ?=> Targets.PreApps
    ==> Targets.Build
    ==> Targets.All

Targets.SdkImage
    ==> Targets.Sdk
Targets.Runtime
    ==> Targets.Sdk

Targets.PullSdk ?=> Targets.PreApps
Targets.PullRuntime ?=> Targets.PreApps

Targets.Build ==> Targets.BuildForTest
Targets.PullApp ==> Targets.BuildForTest 
Targets.PullDb ==> Targets.BuildForTest
Targets.PullSdk ==> Targets.BuildForTest
Targets.PullRuntime ==> Targets.BuildForTest


Targets.PullRuntime ?=> Targets.PreApps
Targets.PullApp ?=> Targets.PreApps

Targets.Runtime ==> Targets.PushApps
Targets.PullApp ==> Targets.PushApps 
Targets.PullDb ==> Targets.PushApps
Targets.PullSdk ==> Targets.PushApps
Targets.PullRuntime ==> Targets.PushApps


Targets.GenericSdk
    ==> Targets.Complete
Targets.All
    ==> Targets.Complete

Targets.Build
|> runOrDefaultWithArguments 