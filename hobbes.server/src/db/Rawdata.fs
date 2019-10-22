namespace Hobbes.Server.Db

open Cache
open FSharp.Data

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
    let private db = 
        Database.Database("rawdata", CacheRecord.Parse)
          .AddView("table")
          .AddView "WorkItemRevisions"

    let keys (source : DataConfiguration.DataSource) = 
        let startKey = 
            sprintf """["%s","%s"]""" source.SourceName source.ProjectName
        let endKey = 
            sprintf """["%s","%s_"]""" source.SourceName source.ProjectName
        startKey,endKey
     
    let InsertOrUpdate doc = 
        db.InsertOrUpdate doc

    let getState id = 
        match db.TryGet id with
        None -> NotStarted
        | Some s -> 
            let state = s.State 
            state |> SyncStatus.Parse

    let setSyncState state message source = 
        let doc = createCacheRecord {
                                       Source = source
                                       Transformations = []
                                    } null state message
        db.InsertOrUpdate(doc) |> ignore
        (doc |> CacheRecord.Parse).Id

    let setSyncFailed message = setSyncState Failed message >> ignore
    let setSyncCompleted = setSyncState Synced None >> ignore
    let createSyncStateDocument = setSyncState Started None

    let tryLatestId (source : DataConfiguration.DataSource) =
        let startKey, endKey = keys source
        try
            let revisions =  
                db.Views.["WorkItemRevisions"].List(WorkItemRevisionRecord.Parse,
                                                         startKey = startKey,
                                                         endKey = endKey
                )
            (revisions
            |> Array.maxBy(fun record -> 
                try
                   record.JsonValue.AsInteger64()
                with e -> 
                   eprintfn "Failed to get revision from record. Reason: %s. Record: %s" e.Message <| record.ToString()
                   -1L
                )
            ).JsonValue.AsInteger64()
            |> Some
        with e ->
           eprintfn "Failed to get last revision. Reason: %s." e.Message 
           None

    let list (source : DataConfiguration.DataSource) = 
        let startKey, endKey = keys source 
        db.Views.["table"].List(TableView.parse,
                                                  startKey = startKey,
                                                  endKey = endKey
        ) |> TableView.toTable
        |> Seq.map(fun (columnName,values) -> 
            columnName, values.ToSeq()
            |> Seq.map(fun (i,v) -> Hobbes.Parsing.AST.KeyType.Create i, v)
        ) |> Hobbes.FSharp.DataStructures.DataMatrix.fromTable
        
    let tryGetRev id = db.TryGetRev id  
    let tryGetHash id = db.TryGetHash id  


