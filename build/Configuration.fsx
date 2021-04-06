#r "paket: //
nuget FSharp.Data //"

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
    type Environment(globalEnvFile) =
        let globalEnvFile = System.IO.Path.GetFullPath globalEnvFile
        let env = 
            (if System.IO.File.Exists globalEnvFile then
                printfn "Loading global env file"
                Env.Load globalEnvFile
             else
                Environment.environVarOrFail "ENV_FILE"
                |> Env.Parse).Data
        member __.AzureDevopsPat with get() = env.AzureDevopsPat |> fromBase64
        member __.MasterUser with get() = env.MasterUser |> fromBase64
        member __.CouchdbUser with get() = env.CouchdbUser |> fromBase64
        member __.CouchdbPassword with get() = env.CouchdbPassword |> fromBase64