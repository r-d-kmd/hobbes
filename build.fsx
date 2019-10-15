open Fake.SystemHelper
#r "paket: 
nuget Fake
nuget Fake.Core
nuget Fake.Core.Target
nuget Fake.DotNet
nuget Fake.DotNet.NuGet
nuget Fake.IO.FileSystem
nuget Fake.DotNet.Cli //"
#load "./.fake/build.fsx/intellisense.fsx"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open Fake.Core
open Fake.DotNet
open Fake.DotNet.NuGet

let run command workingDir args = 
    let arguments = 
        args |> String.split ' ' |> Arguments.OfArgs
    RawCommand (command, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let dockerFiles = System.IO.Directory.EnumerateFiles("./","Dockerfile",System.IO.SearchOption.AllDirectories)
let projFiles =System.IO.Directory.EnumerateFiles("./","*.fsproj",System.IO.SearchOption.AllDirectories)

let build configuration workingDir =
    let args = sprintf "--output ../bin/%s --configuration %s" configuration configuration
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) "build" args 
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" "build" workingDir

Target.create "Build" (fun _ ->
    projFiles
    |> Seq.iter(fun file ->
        let workDir = System.IO.Path.GetDirectoryName file
        build "Debug" workDir
    )
    
)

Target.create "ReleaseBuild" (fun _ ->
    projFiles
    |> Seq.iter(fun file ->
        let workDir = System.IO.Path.GetDirectoryName file
        build "Release" workDir
    )
)

Target.create "Test" (fun _ ->
    run "docker-compose" "." "up --build --force-recreate"
    DotNet.test (DotNet.Options.withWorkingDirectory "./tests") ""
)

let inline (@@) p1 p2 = 
    System.IO.Path.Combine(p1,p2)
let company = "KMD A/S"
let authors = [company; "Rune Lund-Søltoft"; "Lucas Helin Petersen"; "Mikkel Ditlevsen"]
let projectName = "hobbes"
let projectDescription = "A high level language for data transformations and calculations"
let projectSummary = projectDescription
let releaseNotes = "Initial release"
let buildDir = "./src/bin"
let packagingRoot = "./packaging"
let packagingDir = System.IO.Path.Combine(packagingRoot, "hobbes")
let netDir = packagingDir @@ "lib/net45" |> System.IO.Path.GetFullPath

let PackageDependencies =
    [
        "Accord.MachineLearning", "3.8.0"
        "Deedle","2.0.4"
        "FParsec","1.0.3"
    ]
        
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
    CleanDirs [buildDir; packagingRoot; packagingDir; netDir]
    
)
Target.create "CopyFiles" (fun _ -> 
    [
        "dll"
        "pdb"
    ] |> List.map (fun ext ->
            buildDir @@ (sprintf "Release/%s.%s" projectName ext) |> System.IO.Path.GetFullPath
    ) |> List.iter (fun file -> 
        printfn "Copying %s to %s" file netDir
        System.IO.File.Copy(file, System.IO.Path.Combine(netDir,System.IO.Path.GetFileName file))
    )
)

Target.create "PublishPackage" (fun _ ->
    let assemblyVersion = Environment.environVarOrDefault "APPVEYOR_BUILD_VERSION" "0.2.0.1"
    let myAccessKey = Environment.environVarOrDefault "key" ""

    NuGet.NuGet
     (fun p ->
        {p with 
            Authors = authors
            Project = projectName
            Description = projectDescription
            OutputPath = packagingRoot
            Summary = projectSummary
            WorkingDir = packagingDir
            Version = assemblyVersion
            ReleaseNotes = releaseNotes
            Publish = true
            AccessKey = myAccessKey
            Dependencies = PackageDependencies
            }) "src/hobbes.nuspec"
)


Target.create "BuildDocker" (fun _ -> 
    let dockerOrg = "kmdrd"
    let run = run "docker"
     
    printfn "Found docker files: (%A)" dockerFiles
    dockerFiles
    |> Seq.iter(fun path ->
        let workingDir = System.IO.Path.GetDirectoryName path
        
        let build tag = 
            let args = sprintf "build -t %s/hobbes:%s ." dockerOrg tag 
            printfn "Executing: $ docker %s" args
            run workingDir args

        let tag = workingDir.Split([|'/';'\\'|],System.StringSplitOptions.RemoveEmptyEntries) |> Array.last
        build tag
    ) 
)

Target.create "PushToDocker" (fun _ ->
    let dockerOrg = "kmdrd"
    let run = run "docker"
    
    dockerFiles
    |> Seq.iter(fun path ->
        let workingDir = System.IO.Path.GetDirectoryName path
        
        let push tag = 
            let args = sprintf "push %s/hobbes:%s" dockerOrg tag
            printfn "Executing: $ docker %s" args
            run workingDir args
        let tag = workingDir.Split([|'/';'\\'|],System.StringSplitOptions.RemoveEmptyEntries) |> Array.last
      
        push tag
    ) 
)

open Fake.Core.TargetOperators

"Clean"
   ==> "Build"

"Clean"
   ==> "ReleaseBuild"
   ==> "CopyFiles"
   ==> "PublishPackage"

"ReleaseBuild" 
   ==> "CopyFiles"
   ==> "BuildDocker"

Target.runOrDefaultWithArguments "Build"