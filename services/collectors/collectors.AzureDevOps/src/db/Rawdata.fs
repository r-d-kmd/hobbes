#nowarn "3061"
namespace Collector.AzureDevOps.Db

open FSharp.Data
open Hobbes.Web
open Hobbes.Server.Db
open Hobbes.Shared.RawdataTypes

module Rawdata =
    type SyncStatus = 
        Synced
        | Started
        | Failed
        | Updated
        | NotStarted
        with override x.ToString() = 
                match x with
                Synced -> "synced"
                | NotStarted -> "not started"
                | Started -> "started"
                | Updated -> "updated"
                | Failed -> "failed"
             static member Parse (s:string) =
                    match s.ToLower() with
                    "synced" -> Synced
                    | "started" -> Started
                    | "failed" -> Failed
                    | "updated" -> Updated
                    | "not started" -> NotStarted
                    | _ -> 
                        Log.debug (sprintf "Unknown sync state: %s" s)
                        Failed
    [<Literal>]
    let AzureDevOpsSourceString = """{
            "name": "azuredevops",
            "account" : "kmddk",
            "project" : "gandalf",
            "dataset" : "commits"
        }"""

    [<Literal>]
    let AzureDevOpsDataString = """{
                "_id" : "name",
                "source" : """ + AzureDevOpsSourceString + """,
                "data" : {
                    "columnNames" : ["a","b"],
                    "values" : [["zcv","lkj"],[1.2,3.45],["2019-01-01","2019-01-01"]]
                }
        }"""

    type internal AzureDevOpsSource = FSharp.Data.JsonProvider<AzureDevOpsSourceString>
    type internal AzureDevOpsData = FSharp.Data.JsonProvider<AzureDevOpsDataString>

    let source2AzureSource (source : Config.Source) =
        source.JsonValue.ToString() |> AzureDevOpsSource.Parse

    let parseConfiguration doc = 
       let config = Config.Parse doc
       let source = config.Source |> source2AzureSource
       
       if System.String.IsNullOrWhiteSpace source.Project then failwithf "Didn't supply a project %s" doc
       if System.String.IsNullOrWhiteSpace source.Account then failwithf "Account can't be empty %s" doc
       
       config

    type private WorkItemRevisionRecord = JsonProvider<"""
        {
             "id": "07e9a2611a712c808bd422425c9dcda2",
             "key": [
              "Azure DevOps",
              "flowerpot"
             ],
             "value": 90060205
    }""">

    

    let createDataRecord key (data : string) keyValue =
        
        let data = if isNull data then data else data.Replace("\\", "\\\\")
        let timeStamp = System.DateTime.Now.ToString (System.Globalization.CultureInfo.CurrentCulture)
        let record = 
            (sprintf """{
                        "_id" : "%s",
                        "timeStamp" : "%s"
                        %s%s
                    }""" key
                         timeStamp
                         (if data |> isNull then 
                              "" 
                          else 
                              sprintf """, "data": %s""" data)
                          (match keyValue with
                          [] -> ""
                          | values ->
                              System.String.Join(",",
                                  values
                                  |> Seq.map(fun (k,v) -> sprintf """%A:%A""" k v)
                              ) |> sprintf """,%s"""
                         ))

        let cacheRecord = record |> Cache.CacheRecord.Parse

        assert(cacheRecord.Id = key)
        assert(cacheRecord.TimeStamp = timeStamp)

        record

    let createCacheRecord key (data : string) (state : SyncStatus) message cacheRevision =
        let values = 
            [
               if cacheRevision |> Option.isSome then yield "revision", string cacheRevision.Value
               yield "state", string state
               if message |> Option.isSome then yield "message", message.Value
            ]

        createDataRecord key data values

    type private RawList = JsonProvider<"""["id_a","id_b"]""">
    let private db = 
        Database.Database("rawdata", AzureDevOpsData.Parse, Log.loggerInstance)

    let insertOrUpdate doc = 
        async{
            db.InsertOrUpdate doc
            |> Log.logf "Inserted data: %s"
        } |> Async.Start

    let delete (id : string) = 
        200, (db.Delete id).ToString()                  

   

    let getState id = 
        db.TryGet id
        |> Option.bind(fun s -> s.ToString() |> Some)

    let setSyncState state message revision (config : Config.Root) = 
        let doc = createCacheRecord config.Id null state message revision
        db.InsertOrUpdate(doc) |> ignore
        (doc |> Config.Parse).Id

    let setSyncFailed message revision = setSyncState Failed (Some message) (Some revision) >> ignore
    let setSyncCompleted revision = setSyncState Synced None (Some revision) >> ignore
    let updateSync event revision = setSyncState Updated (Some event) (Some revision) >> ignore
    let createSyncStateDocument revision = setSyncState Started None (Some revision)

    let tryLatestId searchKey =
        None //should ideal return the latest workitem id but views in production are unstable

    let list = 
        db.ListIds

    let clear()=
        //todo: make a bulk update instead setting the prop _deleted to true
        async {
            let!_ =
                db.ListIds()
                |> Seq.filter(fun id ->
                   (id.StartsWith "_design" || id = "default_hash") |> not
                )
                |> Seq.map(fun id -> 
                    async { 
                        let status,body = delete id
                        if status > 299 then
                            Log.errorf "" "Couldn't delete Rawdata %s. Message: %s" id body
                        else
                            Log.debugf "Deleted Rawdata %s" id
                    }
                ) |> Async.Parallel
            return ()
        } |> Async.Start
        200,"deleting"
    
    let projectsBySource (config : Config.Root) = 
        //this could be done with a view but the production environment often exceeds the time limit.
        //we haven't got enough documents for a missing index to be a problem and since it's causing problems 
        //reliance on an index has been removed
        let docs = db.List() 
        let configSearchKey = config |> keyFromConfig
        Log.logf "projects by source (%s): %A" (config.Source.JsonValue.ToString()) docs
        let res = 
            docs
            |> Seq.filter(fun doc -> 
               (doc.JsonValue.ToString() |> keyFromConfigDoc) = configSearchKey
            ) 
        Log.logf "Project data found by source %A" res
        res

    let bySource (source : Config.Root) = 
        let data =
            source
            |> projectsBySource
        Log.logf "Rawdata by source: %A" (data |> List.ofSeq)
        let result = 
            data
            |> Seq.collect(fun s -> 
                s.JsonValue.Properties() |> Seq.tryFind(fun (n,_) -> n = "data")
                |> Option.bind(fun _ -> 
                    try
                        let data = s.Data :> obj :?> AzureDevOpsAnalyticsRecord.Root
                        data.Value |> Some
                    with _ -> None
                ) |> Option.orElse (Some Array.empty)
                |> Option.get
            )
        if result |> Seq.isEmpty then None
        else Some result

    let clearProject (config : Config.Root) =
        async {
            let! _ = 
                config
                |> projectsBySource
                |> Seq.map(fun p -> 
                    async {
                        try
                            p.Id 
                            |> delete
                            |> ignore
                        with _ -> ()
                    }
                ) |> Async.Parallel
            return ()
        } |> Async.RunSynchronously