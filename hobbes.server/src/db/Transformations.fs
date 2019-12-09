namespace Hobbes.Server.Db

open FSharp.Data
open Hobbes.Web

module Transformations =
   type TransformationRecord = JsonProvider<"""{"_id" : "jlk", "lines" : ["","jghkhj"]}""">

   let private db = Database.Database ("transformations", TransformationRecord.Parse, Log.loggerInstance) 

   let load (transformationIds : #seq<string>) = 
      db.FilterByKeys transformationIds

   let store doc = db.InsertOrUpdate doc

   let tryGetRev id = db.TryGetRev id

   let tryGetHash id = db.TryGetHash id

   let list() = 
      db.List()

   let compactAndClean() = db.CompactAndClean()