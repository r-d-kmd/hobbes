open Hobbes.Helpers.Environment
open Worker.Git.Reader
open Hobbes.Web
open Hobbes.Messaging.Queue

let synchronize (source : GitSource.Root) token =
        try
            let columnNames,values,rowCount = 
                match source.Dataset.ToLower() with
                "commits" ->
                    let commits = commits source.Account source.Project
                    let columnNames = """["Time","Message","Author"]"""
                    let values =
                        System.String.Join(",",
                            commits
                            |> Seq.map(fun c ->
                                 sprintf """["%s","%s","%s"]""" (c.Time.ToString()) c.Message c.Author
                            )
                        ) |> sprintf "[%s]"
                    columnNames, values, (commits |> Seq.length)
                | "branches" ->
                    let branches = branches source.Account source.Project
                    let columnNames = """["name","CreateionDate","LastCommit","CommitTime","CommitMessage","CommitAuthor"]"""
                    let values =
                        System.String.Join(",",
                            branches
                            |> Seq.map(fun branch ->
                                let c = branch.Commit
                                sprintf """["%s","%b","%b", "%s","%s","%s"]""" branch.Name branch.IsFirstCommit branch.IsLastCommit (c.Time.ToString()) c.Message c.Author
                            )
                        ) |> sprintf "[%s]"
                    columnNames, values, (branches |> Seq.length)
                | ds -> failwithf "Datsaet (%s) not known" ds
            
            sprintf """{
                    "columnNames" : %s,
                    "values" : %s,
                    "rowCount" : %d
                }""" columnNames values rowCount
            |> Some
        with e ->
            Log.excf e "Sync failed due to exception"
            None

let handleMessage sourceDoc =
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
            try
                match Http.post (Http.UniformData Http.Update) id (sprintf """["%s",%s]""" key data) with
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
        watch Queue.Git handleMessage 5000
    } |> Async.RunSynchronously
    
    0