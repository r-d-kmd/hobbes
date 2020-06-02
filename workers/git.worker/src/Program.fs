open Hobbes.Helpers.Environment
open Worker.Git.Reader
open Hobbes.Web
open Hobbes.Messaging.Broker
open Hobbes.Messaging

let synchronize (source : GitSource.Root) token =
    try
        let columnNames,values,rowCount = 
            match source.Dataset.ToLower() with
            "commits" ->
                let commits = commits source.Account source.Project
                let columnNames = [|"Time";"Message";"Author"|]
                let values =
                    commits
                    |> Seq.map(fun c ->
                         [|c.Time.ToString() :> obj; c.Message :> obj; c.Author :> obj |]
                    ) |> Array.ofSeq
                columnNames, values, (commits |> Seq.length)
            | "branches" ->
                let branches = branches source.Account source.Project
                let columnNames = [|"name";"CreateionDate";"LastCommit";"CommitTime";"CommitMessage";"CommitAuthor"|]
                let values =
                    branches
                    |> Seq.map(fun branch ->
                        let c = branch.Commit
                        let msg = 
                            c.Message.Replace("\\","\\\\")
                                     .Replace("\"","\\\"") 
                        [|
                            branch.Name :> obj
                            branch.IsFirstCommit :> obj
                            branch.IsLastCommit :> obj
                            c.Time.ToString() :> obj
                            msg :> obj
                            c.Author :> obj
                        |]
                    ) |> Array.ofSeq
                columnNames, values, (branches |> Seq.length)
            | ds -> failwithf "Datsaet (%s) not known" ds
        
        {
            ColumnNames = columnNames
            Values = values
            RowCount = rowCount
        } : RawdataTypes.DataResult
        |> Some
    with e ->
        Log.excf e "Sync failed due to exception"
        None

let handleMessage message =
    match message with
    Sync sourceDoc -> 
        Log.logf "Received message. %s" sourceDoc
        let key = 
            sourceDoc 
            |>RawdataTypes.keyFromSourceDoc
        try
            let source = sourceDoc |> GitSource.Parse
            let token = 
                if source.Account.ToString() = "kmddk" then
                    env "AZURE_TOKEN_KMDDK" null
                else
                    env "AZURE_TOKEN_TIME_PAYROLL_KMDDK" null

            match synchronize source token with
            None -> 
                Log.logf "Conldn't syncronize. %s %s" sourceDoc token
                false
            | Some data -> 
                let jsonData = FSharp.Json.Json.serializeU data
                try
                    match Http.post (Http.UniformData Http.Update) id (sprintf """["%s",%s]""" key jsonData) with
                    Http.Success _ -> 
                       Log.logf "Data uploaded to cache"
                       true
                    | Http.Error(status,msg) -> 
                        Log.logf "Upload to uniform data failed. %d %s" status msg
                        false
                with e ->
                    Log.excf e "Failed to cache data"
                    false
        with e ->
            Log.excf e "Failed to process message"
            false
    

[<EntryPoint>]
let main _ =
    Database.awaitDbServer()
    Database.initDatabases ["azure_devops_rawdata"]
    
    async{    
        do! awaitQueue()
        Broker.Git handleMessage
    } |> Async.RunSynchronously
    
    0