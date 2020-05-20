#nowarn "3061"
namespace Readers.AzureDevOps

open FSharp.Data
open Hobbes.Web
open Hobbes.Web.RawdataTypes

module Data =
    type internal AzureDevOpsAnalyticsRecord = JsonProvider<"""{
      "@odata.context": "https://analytics.dev.azure.com/kmddk/flowerpot/_odata/v2.0/$metadata#WorkItemRevisions(WorkItemId,WorkItemType,State,StateCategory,Iteration)",
      "timeStamp" : "ojsdfidsj",
      "value": [
        {
        "WorkItemId":3833,
        "RevisedDate":"2016-12-22T10:56:27.87+01:00",
        "IsCurrent":false,
        "IsLastRevisionOfDay":false,
        "WorkItemRevisionSK":62809820,
        "Title":"Manage templates",
        "WorkItemType":"Feature",
        "ChangedDate":"2016-12-22T09:22:40.967+01:00",
        "CreatedDate":"2016-12-19T11:19:08.42+01:00",
        "ClosedDate": "2018-09-13T16:14:24.323+02:00",
        "State":"User stories created",
        "Priority":2,
        "StateCategory":"Resolved",
        "StoryPoints": 2.0,
        "LeadTimeDays" : 200.1,
        "CycleTimeDays" : 9080.122,
        "Area": {
                "AreaPath": "Momentum"
            },
        "Iteration":{
            "IterationName":"Gandalf",
            "Number":159,
            "StartDate": "2017-05-15T00:00:00+02:00",
            "EndDate": "2017-05-28T23:59:59.999+02:00",
            "IterationPath":"Gandalf",
            "IterationLevel1":"Gandalf",
            "IterationLevel2":"Gandalf",
            "IterationLevel3":"Gandalf",
            "IterationLevel4":"Gandalf"
        }},{
        "WorkItemId":3833,
        "WorkItemRevisionSK":62809820,
        "Title":"Manage templates",
        "WorkItemType":"Feature",
        "Iteration":{
            "IterationName":"Gandalf",
            "Number":159,
            "IterationPath":"Gandalf"
        }},{
            "WorkItemId":3833,
            "WorkItemRevisionSK":62809820,
            "Title":"Manage templates",
            "WorkItemType":"Feature"
        }
    ], "@odata.nextLink":"https://analytics.dev.azure.com/"}""">

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

    
    type Value =
       Object of string
       | Int of int 
       | Float of float
       | String of string
       | Array of seq<Value>

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
                              let rec getValue  = 
                                  function
                                      Object s ->
                                          s
                                      | Int i -> 
                                          string i
                                      | Float f ->
                                          string f
                                      | String s ->
                                          sprintf """ "%s" """ s
                                      | Array a ->
                                          System.String.Join(",",a |> Seq.map(getValue))
                                          |> sprintf "[%s]"
                              System.String.Join(",",
                                  values
                                  |> Seq.map(fun (k,v) -> 
                                     v
                                     |> getValue
                                     |> sprintf """ %A : %s """ k
                                  )
                              ) |> sprintf """,%s"""
                         )
            )

        let cacheRecord = record |> Cache.CacheRecord.Parse

        assert(cacheRecord.Id = key)
        assert(cacheRecord.TimeStamp = timeStamp)

        record

    let createCacheRecord key (data : string) (state : SyncStatus) message cacheRevision =
        let values = 
            [
               if cacheRevision |> Option.isSome then yield "revision", cacheRevision.Value |> string |> String
               yield "state", state |> string |> String
               if message |> Option.isSome then yield "message", message.Value |> String
            ]

        createDataRecord key data values

    type private RawList = JsonProvider<"""["id_a","id_b"]""">
    let private db = 
        Database.Database("azure_devops_rawdata", AzureDevOpsData.Parse, Log.loggerInstance)

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
    
    let projectsBySource (source : AzureDevOpsSource.Root) = 
        let configSearchKey = source.JsonValue.ToString() |> keyFromSourceDoc
        let res = 
            db.List() 
            |> Seq.filter(fun doc ->
                let hasData = 
                    doc.JsonValue.Properties() 
                    |> Seq.tryFind(fun (n,_) -> n = "data") 
                    |> Option.isSome
                let hasSource = 
                    doc.JsonValue.Properties() 
                    |> Seq.tryFind(fun (n,_) -> n = "data") 
                    |> Option.isSome
                hasData && hasSource
                && (doc.JsonValue.ToString() |> keyFromConfigDoc) = configSearchKey
            )
        Log.logf "Project data found by source %A" res
        res

    let bySource (source : AzureDevOpsSource.Root) = 
        let data =
            source
            |> projectsBySource
        Log.logf "Rawdata by source: %A" (data |> List.ofSeq)
        let result = 
            data
            |> Seq.collect(fun s -> 
                let hasData = 
                    s.JsonValue.Properties() 
                    |> Seq.tryFind(fun (n,_) -> n = "data") 
                    |> Option.isSome
                if hasData then
                    try
                        let data = s.Data :> obj :?> AzureDevOpsAnalyticsRecord.Root
                        data.Value
                    with _ -> 
                        Array.empty
                else
                    Array.empty
            )
        if result |> Seq.isEmpty then None
        else Some result