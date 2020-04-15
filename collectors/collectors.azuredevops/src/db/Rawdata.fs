#nowarn "3061"
namespace Collector.AzureDevOps.Db

open Hobbes.Server.Db.Cache
open FSharp.Data
open Hobbes.Web
open Hobbes.Server.Db
open Hobbes.Shared.RawdataTypes

module Rawdata =
        
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
        db.TryGet id
        |> Option.bind(fun s -> s.ToString() |> Some)

    let setSyncState state message revision config = 
        let doc = createCacheRecord config null state message revision
        db.InsertOrUpdate(doc) |> ignore
        (doc |> CacheRecord.Parse).Id

    let setSyncFailed message revision = setSyncState Failed (Some message) (Some revision) >> ignore
    let setSyncCompleted revision = setSyncState Synced None (Some revision) >> ignore
    let updateSync event revision = setSyncState Updated (Some event) (Some revision) >> ignore
    let createSyncStateDocument revision = setSyncState Started None (Some revision)

    let tryLatestId (config : Config.Root) =
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
        db.List() 
        |> Seq.filter(fun doc -> 
           doc.Source = config.Source && doc.Project = config.Project
           && (doc.JsonValue.Properties() 
               |> Seq.tryFind(fun (name,v) -> 
                   name = "data" 
               ) |> Option.isSome)
        ) 

    let bySource source = 
        let s =
            source
            |> projectsBySource
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
        if s |> Seq.isEmpty then None
        else Some s

    let clearProject (config : Hobbes.Shared.RawdataTypes.Config.Root) =
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

    let get (id : string) = 
        200, (db.Get id).ToString()