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
let buildConfigurationName = (Environment.environVarOrDefault "CONFIGURATION" "Debug").ToLower()
let buildConfiguration = 
    match buildConfigurationName with
    "release" -> 
        printfn "Using release configuration"
        DotNet.BuildConfiguration.Release
    | _ -> DotNet.BuildConfiguration.Debug
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
                    |> List.map(fun (n,v) -> sprintf "--build-arg %s=\"%s\"" n v)
                )
            ( match file with
              None -> 
                  sprintf "build -t %s %s ."  
              | Some f -> sprintf """build -f "%s" -t %s %s .""" f) (tag.ToLower()) buildArgs
        | Tag(t1,t2) -> sprintf "tag %s %s" t1 t2
    run "docker" dir arguments

let version =
        match buildConfiguration with
        DotNet.BuildConfiguration.Release -> "latest"
        | DotNet.BuildConfiguration.Debug -> "debug"
        | _ -> failwithf "configuration has no specific version. %A" buildConfiguration

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


let commonPath name = 
    sprintf "./common/hobbes.%s/src/hobbes.%s.fsproj" name name
let commons = 
    [
        CommonLib.Web
        CommonLib.Helpers
        CommonLib.Core
        CommonLib.Messaging
    ]
let apps : seq<App*string> = 
    let services = 
        serviceDir.EnumerateFiles("*.fsproj",SearchOption.AllDirectories)
        |> Seq.map(fun file ->
            let workingDir = 
                file.Directory.Parent.FullName
            
            let n = Path.GetFileNameWithoutExtension file.Name
            let name = 
                if n.StartsWith "hobbes." then n.Remove(0,"hobbes.".Length) else n
            Service name, workingDir
        )

    let workers = 
        workerDir.EnumerateFiles("*.fsproj",SearchOption.AllDirectories)
        |> Seq.map(fun file ->
            let workingDir = 
                file.Directory.Parent.FullName
            
            Path.GetFileNameWithoutExtension (file.Name.Split('.') |> Array.head) |> Worker, workingDir
        )
    services |> Seq.append workers

let buildApp (name : string) (appType : string) workingDir =
    
    let tag = name.ToLower()
    
    let build _ = 
        let buildArgs = [(sprintf "%s_NAME" (appType.ToUpper()), name)]
        let tags =
           let t = createDockerTag dockerOrg tag
           [
               t + ":" + assemblyVersion
               t + ":" + "latest"
           ]

        let file = sprintf "%s/Dockerfile.%s" dockerDir.FullName appType |> Some
        docker (Build(file,tag,buildArgs)) workingDir
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
    
    let buildTargetName = tag 
    let pushTargetName = "Push" + tag 
    
    Target.create buildTargetName build
    Target.create pushTargetName push
    buildTargetName ==> pushTargetName ==> "PushApps" |> ignore

Target.create "Complete" ignore
Target.create "PushApps" ignore
Target.create "All" ignore
Target.create "Build" ignore

Target.create "PreApps" ignore
Target.create "BuildForTest" ignore

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

apps
|> Seq.iter(fun (app,dir) ->
    let name,appType = app.NameAndType
    buildApp name appType dir
    "PreApps" ==> name ==> "Build" |> ignore
) 

commons |> List.iter(fun common ->
    let commonName = common.ToString()
    let targetName = commonName
    Target.create targetName (fun _ ->
        let projectFile = commonPath commonName
        package buildConfiguration commonLibDir projectFile
    )
) 

Target.create "GenericSdk" (fun _ ->   
    let tag = sprintf "%s/sdk" dockerOrg
    let build file = 
       docker (Build(Some file,tag,[])) dockerDir.Name
       
    build "Dockerfile.sdk"
    docker (Push tag) dockerDir.Name

    //build the debug version for local use if required
    if buildConfiguration = DotNet.BuildConfiguration.Debug then
        build "Dockerfile.sdk-debug"
)

Target.create "Sdk" (fun _ ->   
    let tag = sprintf "%s/app" dockerOrg
    let file = Some("Dockerfile.app")
    let build configuration = 
        docker (Build(file,tag,["CONFIGURATION",configuration])) dockerDir.Name
    build "Release"
    docker (Push tag) dockerDir.Name

    //build the debug version for local use if required
    if buildConfiguration = DotNet.BuildConfiguration.Debug then
        build "Debug"
)

Target.create "TestNoBuild"(fun _ ->
    run "test" "." "" |> ignore
)

Target.create "Pull" ignore

Target.create "PullRuntime" (fun _ ->
    docker (Pull "mcr.microsoft.com/dotnet/core/aspnet:3.1") "."
)

Target.create "PullSdk" (fun _ ->
    docker (Pull "kmdrd/app") "."
)

Target.create "PullDb" (fun _ ->
    docker (Pull "kmdrd/couchdb") "."
)

"GenericSdk" 
    ?=> "CleanCommon" 
    ==> "Dependencies"
    ==> "Helpers" 
    ==> "Web"
    ==> "Messaging"
    ==> "Sdk"
    ?=> "PreApps"
    ==> "Build"
    ==> "All"

"Sdk" 
    ==> "All"

"PullSdk" ?=> "PreApps"
"PullRuntime" ?=> "PreApps"

"Build" ==> "BuildForTest" 
"PullDb" ==> "BuildForTest"
"PullSdk" ==> "BuildForTest"
"PullRuntime" ==> "BuildForTest"

"GenericSdk"
    ?=> "All"
    ==> "Complete"

Target.runOrDefaultWithArguments "Build"