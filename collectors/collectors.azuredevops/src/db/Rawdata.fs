#nowarn "3061"
namespace Collector.AzureDevOps.Db

open Hobbes.Server.Db.Cache
open FSharp.Data
open Hobbes.Web
open Hobbes.Server.Db

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
        Database.Database("rawdata", CacheRecord.Parse, Log.loggerInstance)
                .AddView "WorkItemRevisions"

    let delete (id : string) = 
        200, (db.Delete id).ToString()                  

    let keys (source : DataConfiguration.DataSource) = 
        let startKey = 
            sprintf """["%s","%s"]""" source.SourceName source.ProjectName
        startKey
     
    let InsertOrUpdate doc = 
        async{
            db.InsertOrUpdate doc |> ignore
        }

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
        None //should ideal return the latest workitem id but view in production are unstable

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
    
    let projectsBySource (source : DataConfiguration.DataSource) = 
        //this could be done with a view but the production environment often exceeds the time limit.
        //we haven't got enough documents for a missing index to be a problem and since it's causing problems 
        //reliance on an index has been removed
        db.List() 
        |> Seq.filter(fun doc -> 
           doc.Source = source.SourceName && doc.Project = source.ProjectName
           && (doc.JsonValue.Properties() 
               |> Seq.tryFind(fun (name,v) -> 
                   name = "data" 
               ) |> Option.isSome)
        ) 

    let bySource source = 
        source
        |> projectsBySource
        |> Seq.collect(fun s -> 
            match s.JsonValue.Properties() |> Seq.tryFind(fun (n,_) -> n = "data") with
            Some _ -> 

                let data = s.Data :> obj :?> AzureDevOpsAnalyticsRecord.Root
                data.Value
            | None -> [||]
        )

    let clearProject (source : DataConfiguration.DataSource) =
        async {
            let! _ = 
                source
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

    let get (id : string) = 
        200, (db.Get id).ToString()