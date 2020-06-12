open Hobbes.Helpers.Environment
open Worker.Git.Reader
open Hobbes.Web
open Hobbes.Messaging.Broker
open Hobbes.Messaging
open Hobbes.Helpers
open Hobbes.Web.Cache

let synchronize (source : GitSource.Root) token =
    try
        let columnNames,values,rowCount = 
            match source.Dataset.ToLower() with
            "commits" ->
                let commits = commits source.Account source.Project
                let columnNames = [|"id";"Time";"Project";"Repository Name";"Branch Name";"Author"|]
                let values =
                    commits
                    |> Seq.distinct
                    |> Seq.map(fun c ->
                         [|
                             c.Id |> Value.Text
                             c.Date |> Value.Date
                             c.Author |> Value.Text
                             c.RepositoryName |> Value.Text
                             c.BranchName |> Value.Text
                             c.Project |> Value.Text
                         |]
                    ) |> Array.ofSeq
                columnNames, values, (commits |> Seq.length)
            | ds -> failwithf "Datsaet (%s) not known" ds
        
        {
            ColumnNames = columnNames
            Values = values
            RowCount = rowCount
        } : Cache.DataResult
        |> Some
    with e ->
        Log.excf e "Sync failed due to exception"
        None

let handleMessage message =
    match message with
    Empty -> Success
    | Sync sourceDoc -> 
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
                sprintf "Conldn't syncronize. %s %s" sourceDoc token
                |> Failure
            | Some data -> 
                let data = 
                    {
                        CacheKey = key
                        TimeStamp = None 
                        Data = data
                    } : Cache.CacheRecord
     
                try
                    match Http.post (Http.UniformData Http.Update) data with
                    Http.Success _ -> 
                       Log.logf "Data uploaded to cache"
                       Success
                    | Http.Error(status,msg) -> 
                        sprintf "Upload to uniform data failed. %d %s. Data: %s" status msg (data |> Json.serialize)
                        |> Failure
                with e ->
                    Log.excf e "Failed to cache data"
                    Excep e
        with e ->
            Log.excf e "Failed to process message"
            Excep e
    

[<EntryPoint>]
let main _ =
    Database.awaitDbServer()
    Database.initDatabases ["azure_devops_rawdata"]
    
    async{    
        do! awaitQueue()
        Broker.Git handleMessage
    } |> Async.RunSynchronously
    
    0