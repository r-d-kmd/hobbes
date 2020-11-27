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

module BuildExtension =

    open Fake.Core
    open Fake.Core.TargetOperators
    open Fake.IO
    open System.IO

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

    let assemblyVersion = Environment.environVarOrDefault "VERSION" "2.0.default"
    let dockerOrg = Environment.environVarOrDefault "DOKCER_ORG" "hobbes.azurecr.io"
    let tagPrefix = "hobbes"
    let createDockerTag dockerOrg (tag : string) = sprintf "%s/%s-%s" dockerOrg tagPrefix (tag.ToLower())

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

    let dockerFilePath = sprintf "%s/Dockerfile.service" dockerDir.FullName
    let sharedFiles = 
        [
            "common/hobbes.messaging/src/Broker.fs"
        ]

    let generateLocalDockerAndSharedFiles name (appType : string) workingDir =
        
        let srcDir = Path.Combine(workingDir,"src")
        let copyTempFiles _ =
            //read the docker file template
            let dockerFile = 
                dockerFilePath
                |> File.ReadAllText
            let localDockerFile = sprintf "%s/Dockerfile" workingDir

            //substitute some placeholders in dockerfile
            let content = 
                match appType.ToLower() with
                "service" -> 
                    dockerFile.Replace("${SERVICE_NAME}",name)
                | "worker" ->
                    dockerFile.Replace("${SERVICE_NAME}",name + ".worker")
                | _ -> failwithf "Don't know app type %s" appType

            let preamble = 
                (sprintf """# This is a temporary file do not edit
                            # edit %s instead
                         """ dockerFilePath)
                
            File.WriteAllText(localDockerFile,preamble + content)
            //setAttributes dockerFilePath localDockerFile

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
            )
        printfn "Then this happened"

        let targetName =
            name.ToLower() 
            |> sprintf "CopyTempFiles%s"

        Target.create targetName copyTempFiles 

        targetName ==> "PreApps" |> ignore     
        

    let tagAndCleanUpFiles (name : string) appType workingDir =
        let tag = name.ToLower()
        let previousTag =
            match appType with
            |"service" -> tag
            |"worker" -> tag + ".worker"
            | _ -> failwithf "Don't know app type %s" appType
        let t = createDockerTag dockerOrg tag
        let srcDir = Path.Combine(workingDir,"src")

        let tag _ =
            [
                tagPrefix + "-" + tag
                t + ":" + assemblyVersion
                t + ":" + "latest"
                if appType = "worker" then tag
            ]
            |> List.iter(fun t -> 
                let arguments = 
                    let args = (sprintf "tag %s %s" previousTag t).Split([|' '|], System.StringSplitOptions.RemoveEmptyEntries)
                    System.String.Join(" ",args) 
                run "docker" workingDir (arguments.Replace("  "," ").Trim())
            )

        let deleteTempFiles _ =
            //clean up temporarily copied shared files
            let localDockerFile = sprintf "%s/Dockerfile" workingDir

            sharedFiles
            |> List.iter(fun f ->
                Path.Combine(srcDir,Path.GetFileName(f))
                |> File.Delete
            )
            File.Delete localDockerFile

        let deleteName =
            name.ToLower() 
            |> sprintf "DeleteTempFiles%s"

        Target.create deleteName deleteTempFiles 

        let tagName =
            name.ToLower() 
            |> sprintf "Tag%s"

        Target.create tagName tag 

        "PostApps" ==> tagName ==> deleteName ==> "Build" |> ignore
       

    apps
    |> Seq.iter(fun (app,dir) ->
        let name,appType = app.NameAndType
        generateLocalDockerAndSharedFiles name appType dir
        tagAndCleanUpFiles name appType dir
    )