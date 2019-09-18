#r "paket: 
nuget Fake
nuget Fake.Core
nuget Fake.Core.Target
nuget Fake.DotNet
nuget Fake.DotNet.NuGet
nuget Fake.IO.FileSystem
nuget Fake.DotNet.Cli //"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open Fake.Core
open Fake.IO
open Fake.Core
open Fake.DotNet

let serverPath = Path.getFullName "./"

let clientDeployPath = Path.combine serverPath "deploy"
let deployDir = Path.getFullName "./deploy"
let buildDir = "./bin"

Target.create "Clean" (fun _ ->
    [ deployDir
      clientDeployPath ]
    |> Shell.cleanDirs
)

let runDotNet cmd workingDir args =
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd args
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir    

let build configuration workingDir =
    let args = sprintf "--output ../bin/%s --configuration %s" configuration configuration
    runDotNet "build" workingDir args

Target.create "Build" (fun _ ->
    build "Debug" serverPath
)

Target.create "Bundle" (fun _ ->
    let serverDir = Path.combine deployDir "Server"

    let publishArgs = sprintf "publish -c Release -o \"%s\"" serverDir
    runDotNet publishArgs serverPath ""
)

Target.create "BuildImage" (fun _ ->
    let arguments = "build -t hobbes ." |> String.split ' ' |> Arguments.OfArgs
    Command.RawCommand ("docker", arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory "."
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore
)

open Fake.Core.TargetOperators
"Bundle" 
    ==> "Build"

"Clean" 
    ==> "Build"

"Build"
    ==> "BuildImage"

Target.runOrDefaultWithArguments "BuildImage"