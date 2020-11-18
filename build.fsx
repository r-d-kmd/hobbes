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
open System.IO

[<RequireQualifiedAccess>]
type Targets = 
   Builder
   | PreApps
   | Build
   | All
   | PushApps
   | Generic of string

let targetName = 
    function
       | Targets.Builder -> "Builder"
       | Targets.PreApps -> "PreApps"
       | Targets.Build -> "Build"
       | Targets.All -> "All"
       | Targets.PushApps -> "PushApps"
       | Targets.Generic s -> s

let argFeed = 
    match "FEED_PAT" |> Environment.environVarOrNone  with
    None -> failwith "No PAT for the nuget feed was provided"
    | Some argFeed -> 
        argFeed

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

let dockerOrg = "hobbes.azurecr.io"
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
//let buildConfigurationName = (Environment.environVarOrDefault "CONFIGURATION" "Debug").ToLower()
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

let assemblyVersion = Environment.environVarOrDefault "VERSION" "2.0.default"

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
let verbosity = Normal
    
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
    let srcDir = Path.Combine(workingDir,"src")
    let build _ = 
        
        let tags =
           let t = createDockerTag dockerOrg tag
           [
               "hobbes-" + tag
               t + ":" + assemblyVersion
               t + ":" + "latest"
           ]

        let dockerFilePath = sprintf "%s/Dockerfile.service" dockerDir.FullName
        //read the docker file template
        let file = 
            dockerFilePath
            |> File.ReadAllText
        //substitute some placeholders in dockerfile
        let content = 
            match appType.ToLower() with
            "service" -> 
                file.Replace("${SERVICE_NAME}",name)
            | "worker" ->
                file.Replace("${SERVICE_NAME}",name + ".worker")
            | _ -> failwithf "Don't know app type %s" appType

        let setAttributes orgFile destFile = 
            let attributes = File.GetAttributes(orgFile)
            File.SetAttributes(destFile,FileAttributes.ReadOnly ||| FileAttributes.Temporary ||| attributes)

        let localDockerFile = sprintf "%s/Dockerfile" workingDir
        let preamble = 
            (sprintf """# This is a temporary file do not edit
# edit %s instead
""" dockerFilePath)
        
        File.WriteAllText(localDockerFile,preamble + content)
        setAttributes dockerFilePath localDockerFile

        let sharedFiles = 
            [
                "common/hobbes.messaging/src/Broker.fs"
            ] 

        //copy shared files
        sharedFiles
        |> List.iter(fun f ->
            let content = 
                "//This is a temporary build file and should not be altered"::
                (sprintf "//If changes are need edit %s" f)::
                 (File.ReadAllLines f
                  |> List.ofArray)
            let destFile = Path.Combine(srcDir, Path.GetFileName f)
            File.WriteAllLines(destFile,content)
            setAttributes f destFile
        )        

        docker (Build(None,tag,[])) workingDir
        //clean up temporarily copied shared files
        sharedFiles
        |> List.iter(fun f ->
            Path.Combine(srcDir,Path.GetFileName(f))
            |> File.Delete
        )
        File.Delete localDockerFile
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


create Targets.PushApps ignore
create Targets.All ignore
create Targets.Build ignore
create Targets.PreApps ignore


apps
|> Seq.iter(fun (app,dir) ->
    let name,appType = app.NameAndType
    buildApp name appType dir
    Targets.PreApps ==> Targets.Generic(name) ==> Targets.Build |> ignore
) 
let paket workDir args = run "dotnet" workDir ("paket " + args)

create Targets.Builder (fun _ ->   
    let tag = "builder"
    let file = Some("docker/Dockerfile.builder")
    docker (Build(file,tag, ["FEED_PAT_ARG", argFeed])) "."
)

Targets.Builder ==> Targets.PushApps
Targets.Builder ?=> Targets.PreApps
Targets.Build ==> Targets.All
Targets.Builder ==> Targets.All

Targets.Build
|> runOrDefaultWithArguments 