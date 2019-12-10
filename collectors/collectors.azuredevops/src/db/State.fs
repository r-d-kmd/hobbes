namespace Hobbes.AzureDevopsCollector.Db

open Hobbes.Web
open Hobbes.Server.Db.Cache

module State =

    let private db = 
            Database.Database("state", CacheRecord.Parse, Log.loggerInstance)

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