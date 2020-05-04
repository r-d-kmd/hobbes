namespace Hobbes.Web

open FSharp.Data

module Cache =
    [<Literal>]
    let internal DataResultString = 
        """{
             "columnNames" : ["kjkl","kjlkj"],
             "rows" : [["dsfsd","kjlk"],[2.0,1.3]],
             "rowCount" : 2
        }"""

    [<Literal>]
    let internal CacheRecordString = 
        """{
            "_id" : "name",
            "timeStamp" : "24-09-2019",
            "data" : """ + DataResultString + """
        }"""

    type internal DataResult = JsonProvider<DataResultString>
    type internal CacheRecord = JsonProvider<CacheRecordString>

    let internal hash (input : string) =
            use md5Hash = System.Security.Cryptography.MD5.Create()
            let data = md5Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input))
            let sBuilder = System.Text.StringBuilder()
            (data
            |> Seq.fold(fun (sBuilder : System.Text.StringBuilder) d ->
                    sBuilder.Append(d.ToString("x2"))
            ) sBuilder).ToString()

    let internal key source = 
        source |> hash
     
    let internal createCacheRecord key data =
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

        let cacheRecord = record |> CacheRecord.Parse

        assert(cacheRecord.Id = key)
        assert(cacheRecord.TimeStamp = timeStamp)

        record

    type Cache(name) = 
        let db = 
            Database.Database(name + "Cache", CacheRecord.Parse, Log.loggerInstance)
        
        
        member __.InsertOrUpdate doc = 
            async{
                db.InsertOrUpdate doc
                |> Log.logf "Inserted data: %s"
            } |> Async.Start
        
        member __.Get (confDoc : string) = 
            confDoc
            |> hash
            |> db.TryGet