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

Target.create "BuildImage" (fun _ ->
    let arguments = "build -t front ." |> String.split ' ' |> Arguments.OfArgs
    RawCommand ("docker", arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory "."
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore
)

Target.runOrDefaultWithArguments "BuildImage"