#nowarn "3061"
namespace Hobbes.UniformData

open FSharp.Data
open Hobbes.Web
open Hobbes.Shared.RawdataTypes
open Hobbes.Shared.CommonFunctions
open Hobbes.Helpers.Environment

module TranformationCache =

    let private db = 
        Database.Database("TransformationCache", CacheRecord.Parse, Log.loggerInstance)
    db.Init() |> ignore
    
    let insertOrUpdate doc = 
        async{
            db.InsertOrUpdate doc
            |> Log.logf "Inserted data: %s"
        } |> Async.Start
    
    let get (confDoc : string) = 
        confDoc
        |> hash
        |> db.TryGet