module Transformations

open FSharp.Data

type TransformationRecord = JsonProvider<"""{"_id" : "jlk", "lines" : ["","jghkhj"]}""">

let private db = Database.Database ("transformations", TransformationRecord.Parse)

let load (transformationIds : #seq<string>) = 
   db.FilterByKeys transformationIds

let store doc = db.InsertOrUpdate doc