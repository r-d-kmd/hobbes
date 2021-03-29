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

module BuildGeneral = 
    open Fake.Core
    open Fake.DotNet
    open Fake.IO
    open System.IO

    [<RequireQualifiedAccess>]
    type Targets = 
       Builder
       | PreApps
       | PostApps
       | Build
       | All
       | PushApps
       | Generic of string

    let targetName = 
        function
           | Targets.Builder -> "Builder"
           | Targets.PreApps -> "PreApps"
           | Targets.PostApps -> "PostApps"
           | Targets.Build -> "Build"
           | Targets.All -> "All"
           | Targets.PushApps -> "PushApps"
           | Targets.Generic s -> s

    let ignoreLines =
        File.ReadAllLines ".buildignore"
        |> Seq.fold(fun lines l ->
            if Directory.Exists l then
               (DirectoryInfo(l).FullName)::lines
            elif File.exists l then
                (FileInfo(l).FullName)::lines
            else
               lines
        ) ([])
    printfn "Ignores: %A" ignoreLines
    let ignores fileOrDir = 
        let fullName = 
            if Directory.Exists fileOrDir then
                DirectoryInfo(fileOrDir).FullName |> Some
            elif File.exists fileOrDir then
                FileInfo(fileOrDir).FullName |> Some
            else
                None
        match fullName with
        None -> 
            printfn "Couldn't find %s and couldn't ignore" fileOrDir
            false
        | Some f ->
            printfn "Should ignore %s?" f
            ignoreLines
            |> List.exists(fun d -> 
               if f = d then true
               elif Path.isDirectory d then
                   if File.Exists(f) then
                       FileInfo(f).FullName.StartsWith d 
                   else
                       false
               else
                 if fileOrDir.Contains "calvin" then
                           failwithf "Calvin (%s) wasn't ignored here. %A" fileOrDir ignoreLines 
                 false
            )
            

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
                        |> List.map(fun (n,v) -> 
                            let v = if v = "" then "\"\"" else v
                            sprintf "--build-arg %s=%s" n v)
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

    let feedPat = 
        match "FEED_PAT" |> Environment.environVarOrNone  with
        None -> 
            eprintfn "No PAT for the nuget feed was provided"
            ""
        | Some argFeed -> 
            argFeed

    let assemblyVersion = Environment.environVarOrDefault "VERSION" "2.0.default"
    let dockerOrg = Environment.environVarOrDefault "DOKCER_ORG" "hobbes.azurecr.io" //Change to docker hub
    let createDockerTag dockerOrg (tag : string) = sprintf "%s/%s" dockerOrg (tag.ToLower())
    //Set to 'Normal' to have more information when trouble shooting 
    let verbosity = Normal

    open System.IO

    let projects : seq<string*string> = 
        let enumerateProjectFiles (dir : DirectoryInfo) =
            
            dir.EnumerateFiles("*.?sproj",SearchOption.AllDirectories)
            |> Seq.filter(fun n ->
                let name = n.Name.ToLower()
                name.EndsWith(".tests.fsproj") |> not && //exclude test projects
                name.EndsWith(".tests.csproj") |> not && //exclude test projects
                ignores n.FullName |> not
            ) 

        DirectoryInfo "./"
        |> enumerateProjectFiles
        |> Seq.map(fun file ->
            let workingDir = 
                file.Directory.Parent.FullName
            
            let name = Path.GetFileNameWithoutExtension file.Name
            name, workingDir
        )

    let buildProject (name : string) workingDir =
        
        let tag = name.ToLower()
        let build _ = 
            
            let tags =
               let t = createDockerTag dockerOrg tag
               [
                   tag
                   t + ":" + assemblyVersion
                   t + ":" + "latest"
               ]

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


    create Targets.PushApps ignore
    create Targets.All ignore
    create Targets.Build ignore
    create Targets.PreApps ignore
    create Targets.PostApps ignore


    projects
    |> Seq.iter(fun (name,dir) ->
        buildProject name dir
        Targets.PreApps ==> Targets.Generic(name) ==> Targets.PostApps ==> Targets.Build |> ignore
    ) 

    create Targets.Builder (fun _ ->
        let path = "docker/Dockerfile.builder"
        if File.exists(path)
        then
            let tag = "builder"
            let file = Some(path)
            docker (Build(file,tag, ["FEED_PAT_ARG", feedPat])) "."
    )

    Targets.Builder ==> Targets.PushApps
    Targets.Builder ?=> Targets.PreApps
    Targets.Build ==> Targets.All
    Targets.Builder ==> Targets.All
    Targets.PreApps ==> Targets.PostApps