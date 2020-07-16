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
                             c.Id |> Value.Create
                             c.Date |> Value.Create
                             c.Project |> Value.Create
                             c.RepositoryName |> Value.Create
                             c.BranchName |> Value.Create
                             c.Author |> Value.Create
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
                let data = Cache.createCacheRecord key [] data
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
    
    async{    
        do! awaitQueue()
        Broker.Git handleMessage
    } |> Async.RunSynchronously
    
    0