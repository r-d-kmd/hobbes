namespace Hobbes.Collector.Db

open FSharp.Data

module Transformations =

    type TransformationRecord = JsonProvider<"""{"_id" : "jlk", "lines" : ["","jghkhj"]}""">

    let private db = 
            Database.Database("transformations", TransformationRecord.Parse, Log.loggerInstance)

    let InsertOrUpdate doc = 
        db.InsertOrUpdate doc    