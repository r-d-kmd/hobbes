#nowarn "3061"
namespace Hobbes.UniformData

open FSharp.Data
open Hobbes.Web
open Hobbes.Shared.RawdataTypes
open Hobbes.Helpers.Environment

module UniformData =

    type private DataRecord = JsonProvider<DataResultString>

    [<Literal>]
    let private UniformRecord = 
        """{
            "_id" : "lkjlkjkl",
            "timeStamp" : "2018/05/01",
            "data" : """ + DataResultString + """
        }
        """
    type internal UniformDataCacheRecord = JsonProvider<UniformRecord>

    let private key source = 
        source |> hash
     
    let private createCacheRecord key data =
        //fail if the data is invalid in form
        data |> DataRecord.Parse |> ignore
        
        let timeStamp = System.DateTime.Now.ToString (System.Globalization.CultureInfo.CurrentCulture)
        let record = 
            sprintf """{
                        "_id" : "%s",
                        "timeStamp" : "%s"
                        "data" : %s
                    }""" key
                         timeStamp
                         data

        let cacheRecord = record |> CacheRecord.Parse

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