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
                         [|c.Time |> Cache.Date; c.Message |> Cache.String; c.Author |> Cache.String |]
                    ) |> Array.ofSeq
                columnNames, values, (commits |> Seq.length)
            | ds -> failwithf "Datsaet (%s) not known" ds
        
        {
            ColumnNames = columnNames
            Rows = values
            RowCount = rowCount
        } : Cache.DataResult
        |> Some
    with e ->
        Log.excf e "Sync failed due to exception"
        None

let handleMessage message =
    match message with
    Empty -> true
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
                Log.logf "Conldn't syncronize. %s %s" sourceDoc token
                false
            | Some data -> 
                let jsonData  = 
                    ({
                        CacheKey = key
                        TimeStamp = None 
                        Data = data
                    } : Cache.CacheRecord) |> FSharp.Json.Json.serializeU

                try
                    match Http.post (Http.UniformData Http.Update) id jsonData with
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