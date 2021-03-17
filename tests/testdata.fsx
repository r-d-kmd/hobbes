#r "paket: //
nuget FSharp.Data //
nuget Fake ~> 5 //
nuget Fake.Core ~> 5 //
nuget Fake.Core.Target  //
nuget Fake.DotNet //
nuget Fake.DotNet.AssemblyInfoFile //
nuget Fake.DotNet.Cli //
nuget Fake.DotNet.NuGet //
nuget Fake.IO.FileSystem //
nuget Fake.Tools.Git ~> 5 //"
#load "./.fake/tests.fsx/intellisense.fsx"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open Fake
open Fake.Core
open FSharp.Data
let fromBase64 s =
    System.Convert.FromBase64String s
    |> System.Text.Encoding.Default.GetString

type Env = JsonProvider<"""../env.JSON""">
let env = Env.GetSample()
let dbUser = env.Data.MasterUser |> fromBase64
let dbPwd = ""

let request httpMethod url = 
    Http.RequestString(url,
        httpMethod = httpMethod,
        silentHttpErrors = true,
        headers = [HttpRequestHeaders.BasicAuth dbUser dbPwd]
    )
request "get" "http://localhost:8080/data/json/azureDevops.Flowerpot.uniformWorkItems"
|> IO.File.writeString false "testdata.json"