namespace Hobbes.Server.Db

open Cache
open FSharp.Data

module Rawdata =
        
    type AzureDevOpsAnalyticsRecord = JsonProvider<"""{
      "@odata.context": "https://analytics.dev.azure.com/kmddk/flowerpot/_odata/v2.0/$metadata#WorkItemRevisions(WorkItemId,WorkItemType,State,StateCategory,Iteration)",
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
        "State":"User stories created",
        "Priority":2,
        "StateCategory":"Resolved",
        "LeadTimeDays" : 200.1,
        "CycleTimeDays" : 9080.122,
        "Iteration":{
            "IterationName":"Gandalf",
            "Number":159,
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

    type private WorkItemRevisionRecord = JsonProvider<"""
        {
             "id": "07e9a2611a712c808bd422425c9dcda2",
             "key": [
              "Azure DevOps",
              "flowerpot"
             ],
             "value": 90060205
    }""">

    type private RawList = JsonProvider<"""["id_a","id_b"]""">
    let private db = 
        Database.Database("rawdata", CacheRecord.Parse, Log.loggerInstance, "localhost:5984")
          .AddView("table")
          .AddView "WorkItemRevisions"

    let keys (source : DataConfiguration.DataSource) = 
        let startKey = 
            sprintf """["%s","%s"]""" source.SourceName source.ProjectName
        startKey
     
    let InsertOrUpdate doc = 
        db.InsertOrUpdate doc

    let getState id = 
        match db.TryGet id with
        None -> "not stated"
        | Some s -> s.ToString()

    let setSyncState state message revision source = 
        let doc = createCacheRecord {
                                       Source = source
                                       Transformations = []
                                    } null state message revision
        db.InsertOrUpdate(doc) |> ignore
        (doc |> CacheRecord.Parse).Id

    let setSyncFailed message revision = setSyncState Failed (Some message) (Some revision) >> ignore
    let setSyncCompleted revision = setSyncState Synced None (Some revision) >> ignore
    let updateSync event revision = setSyncState Updated (Some event) (Some revision) >> ignore
    let createSyncStateDocument revision = setSyncState Started None (Some revision)

    let tryLatestId (source : DataConfiguration.DataSource) =
        let startKey = keys source
        try
            let revisions =  
                db.Views.["WorkItemRevisions"].List(WorkItemRevisionRecord.Parse,
                                                         startKey = startKey
                )
            (revisions
            |> List.maxBy(fun record -> 
                try
                   record.JsonValue.AsInteger64()
                with e -> 
                   Log.errorf e.StackTrace "Failed to get revision from record. Reason: %s. Record: %s" e.Message <| record.ToString()
                   -1L
                )
            ).JsonValue.AsInteger64()
            |> Some
        with e ->
           Log.errorf e.StackTrace "Failed to get last revision. Reason: %s." e.Message 
           None

    let list = 
        db.ListIds

    type private Table = JsonProvider<"""{
          "total_rows": 2,
          "offset": 0,
          "rows": [
            {
              "id": "azure devops:flowerpot",
              "key": [
                "Azure DevOps",
                "flowerpot"
              ],
              "value": "azure devops:flowerpot"
            }
          ]
        }""">

    let bySource source = 
        let key = keys source
        let keys = db.Views.["table"].List(Table.Parse, startKey = key)
        let docs = db.FilterByKeys keys
        docs
        |> Seq.collect(fun s -> 
            match s.JsonValue.Properties() |> Seq.tryFind(fun (n,_) -> n = "data") with
            Some _ -> 
                let data = s.Data.ToString() 
                let value = 
                    (data 
                     |> AzureDevOpsAnalyticsRecord.Parse).Value
                value
            | None -> [||])
            
    let get (id : string) = 
        200, (db.Get id).ToString()

    let delete (id : string) = 
        200, (db.Delete id).ToString()