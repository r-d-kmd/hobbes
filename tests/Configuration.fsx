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

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open FSharp.Data

let fromBase64 s =
    (System.Convert.FromBase64String s
     |> System.Text.Encoding.Default.GetString).Trim()
module Environment = 
    type private Env = JsonProvider<"""{ 
        "apiVersion": "v1", 
        "kind": "Secret",
        "metadata": {
          "name": "env"
        },
        "type": "Opaque",
        "data": {
          "KEY_SUFFIX": "jlajsdflkajsdfl",
          "COUCHDB_PASSWORD": "jlajsdflkajsdfl",
          "COUCHDB_USER": "jlajsdflkajsdfl",
          "SERVER_PORT": "jlajsdflkajsdfl",
          "MASTER_USER": "jlajsdflkajsdfl",
          "RABBIT_HOST": "jlajsdflkajsdfl",
          "RABBIT_PORT": "jlajsdflkajsdfl",
          "RABBIT_USER": "jlajsdflkajsdfl",
          "RABBIT_PASSWORD": "jlajsdflkajsdfl",
          "AZURE_DEVOPS_PAT": "lksdjaflkj"
        }
      }""">
    type Environment private(azureDevopsPat,masterUser, dbUser,dbPwd) =
        member __.AzureDevopsPat with get() = azureDevopsPat
        member __.MasterUser with get() = masterUser
        member __.CouchdbUser with get() = dbUser
        member __.CouchdbPassword with get() = dbPwd

        new(globalEnvFile : string) =
            let globalEnvFile = System.IO.Path.GetFullPath globalEnvFile
            match Fake.Core.Environment.environVarOrNone "AZURE_DEVOPS_PAT" with
            None ->
              let env = 
                if System.IO.File.Exists globalEnvFile then
                    Env.Load globalEnvFile
                else
                    Fake.Core.Environment.environVarOrFail "ENV_FILE"
                    |> Env.Parse
              Environment(env.Data.AzureDevopsPat |> fromBase64,
                          env.Data.MasterUser |> fromBase64,
                          env.Data.CouchdbUser |> fromBase64,
                          env.Data.CouchdbPassword |> fromBase64)
            | Some pat ->
                Environment(pat,
                        Fake.Core.Environment.environVarOrFail "MASTER_USER",
                        Fake.Core.Environment.environVarOrFail "COUCHDB_USER",
                        Fake.Core.Environment.environVarOrFail "COUCHDB_PASSWORD")