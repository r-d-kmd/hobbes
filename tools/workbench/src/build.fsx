open Fake.SystemHelper
#r "paket: groupref build //"
#load "./.fake/build.fsx/intellisense.fsx"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open Fake.Core
open Fake.DotNet
open Fake.IO

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


let dockerFiles = System.IO.Directory.EnumerateFiles("./","Dockerfile",System.IO.SearchOption.AllDirectories)


Target.create "Restore" (fun _ ->
    DotNet.exec (DotNet.Options.withWorkingDirectory ".") "tool restore" |> ignore
    DotNet.exec (DotNet.Options.withWorkingDirectory ".") "paket restore" |> ignore
    run "fake" "../../" "build --target ReleaseBuild"
)

let build configuration _ =
    let args = sprintf "--output ./bin/%s --configuration %s" configuration configuration
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory ".") "build" args 

    if result.ExitCode <> 0 then failwithf "'dotnet build' failed"

Target.create "Build" (build "Release")

Target.create "Run" (fun p ->
    let args = 
        match p.Context.Arguments with
        [] -> ""
        | args ->
            System.String.Join(" ", args)
    run "dotnet" "." (sprintf "run --%s" args)
)


open Fake.Core.TargetOperators
"Restore"
    ==> "Build"
    ==> "Run"

Target.runOrDefaultWithArguments "Build"
