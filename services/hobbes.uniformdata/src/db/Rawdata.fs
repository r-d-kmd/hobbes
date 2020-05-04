#nowarn "3061"
namespace Hobbes.UniformData

open FSharp.Data
open Hobbes.Web
open Hobbes.Shared.RawdataTypes
open Hobbes.Helpers.Environment

module UniformData =
     
    let private createCacheRecord key data =
        //fail if the data is invalid in form
        data |> DataResult.Parse |> ignore
        
        let timeStamp = System.DateTime.Now.ToString (System.Globalization.CultureInfo.CurrentCulture)
        let record = 
            sprintf """{
                        "_id" : "%s",
                        "timeStamp" : "%s"
                        "data" : %s
                    }""" key
                         timeStamp
                         data

        let cacheRecord = record |> UniformDataCacheRecord.Parse

        assert(cacheRecord.Id = key)
        assert(cacheRecord.TimeStamp = timeStamp)

        record

    let private db = 
        Database.Database("uniform", UniformDataCacheRecord.Parse, Log.loggerInstance)

    let insertOrUpdate doc = 
        async{
            db.InsertOrUpdate doc
            |> Log.logf "Inserted data: %s"
        } |> Async.Start
    
    let get (confDoc : string) = 
        confDoc
        |> hash
        |> db.TryGet