open Hobbes.Helpers.Environment
open Worker.Git.Reader
open Hobbes.Web
open Hobbes.Messaging.Broker
open Hobbes.Messaging
open Hobbes.Helpers
open Hobbes.Web.Cache

let synchronize (source : GitSource.Root) token =
    try
        let time = System.DateTime.Now.ToString()
        let columnNames,values,rowCount = 
            match source.Dataset.ToLower() with
            "commits" ->
                let commits = commits source.Account source.Project
                let columnNames = [|"id"; "TimeStamp"; "Time";"Project";"Repository Name";"Branch Name";"Author"|]
                let values =
                    commits
                    |> Seq.distinct
                    |> Seq.map(fun c ->
                         [|
                             c.Id |> box
                             time |> box
                             c.Date |> box
                             c.Project |> box
                             c.RepositoryName |> box
                             c.BranchName |> box
                             c.Author |> box
                         |]
                    ) |> Array.ofSeq
                columnNames, values, (commits |> Seq.length)
            | "releases" ->
                let releaseCommits =  releaseBranches source.Account source.Project
                let columnNames = [|"id";"Time";"Project";"Repository Name";"Branch Name";"Author"|]
                let values =
                    releaseCommits
                    |> Seq.map(fun b ->
                         let c = b.Commit
                         [|
                             c.Id |> box
                             c.Date |> box
                             c.Project |> box
                             c.RepositoryName |> box
                             c.BranchName |> box
                             c.Author |> box
                         |]
                    ) |> Array.ofSeq
                columnNames, values, (releaseCommits |> Seq.length)
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
                    match Http.post (Http.UniformData Http.Update) (data.ToString()) with
                    Http.Success _ -> 
                       Log.logf "Data uploaded to cache"
                       Success
                    | Http.Error(status,msg) -> 
                        sprintf "Upload to uniform data failed. %d %s. Data: %s" status msg (data.ToString())
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