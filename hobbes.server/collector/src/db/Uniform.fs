namespace Hobbes.Collector.Db

open FSharp.Data

module Uniform =

    type UniformRecord = JsonProvider<"""{"nothing" : "nothing"}""">

    let private db = 
            Database.Database("uniform", UniformRecord.Parse, Log.loggerInstance)

    let InsertOrUpdate doc = 
        db.InsertOrUpdate doc    