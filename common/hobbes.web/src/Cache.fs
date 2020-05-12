namespace Hobbes.Web

open FSharp.Data
open Hobbes.Helpers.Environment

module Cache =
    [<Literal>]
    let internal DataResultString = """ {
            "columnNames" : ["a","b"], 
            "rows" : [[0,"hk",null,2.,3,4,"2019-01-01"],
                      [0.4,1.2,2.4,3.5,4.1],
                      ["x","y","z"],
                      ["2019-01.01","2019-01.01"]
                     ],
            "rowCount" : 4
        } """

    [<Literal>]
    let internal CacheRecordString = 
        """{
            "_id" : "name",
            "timeStamp" : "24-09-2019",
            "data" : """ + DataResultString + """
        }"""

    type DataResult = JsonProvider<DataResultString>
    type CacheRecord = JsonProvider<CacheRecordString>


    let key (source : string) = 
        let whitespaceToRemove = [|' ';'\t';'\n';'\r'|]
        source.Split(whitespaceToRemove,System.StringSplitOptions.RemoveEmptyEntries)
        |> System.String.Concat
        |> hash
        
    let createCacheRecord key data =
        //fail if the data is invalid in form
        data |> DataResult.Parse |> ignore
        
        let timeStamp = System.DateTime.Now.ToString (System.Globalization.CultureInfo.CurrentCulture)
        let record = 
            sprintf """{
                        "_id" : "%s",
                        "timeStamp" : "%s",
                        "data" : %s
                    }""" key
                         timeStamp
                         data

        let cacheRecord = record |> CacheRecord.Parse

        assert(cacheRecord.Id = key)
        assert(cacheRecord.TimeStamp = timeStamp)

        cacheRecord

    let readData (record : CacheRecord.Root) = 
        let data = 
            record.Data
        let columnNames = data.ColumnNames
        
        data.Rows
        |> Seq.mapi(fun index row ->
            index,row.JsonValue.AsArray()
                  |> Seq.map(fun v ->
                      match v with
                      JsonValue.String s -> box s
                      | JsonValue.Null -> null
                      | JsonValue.Number n -> box n
                      | JsonValue.Float f -> box f
                      | JsonValue.Boolean b -> box b
                      | v -> failwithf "Only simple values expected but got %A" v
                  ) |> Seq.zip columnNames
        )

    type ICacheProvider = 
         abstract member InsertOrUpdate : CacheRecord.Root -> unit
         abstract member Get : string -> CacheRecord.Root option

    type Cache private(provider : ICacheProvider) =
        static let parser = CacheRecord.Parse
        new(name) =  
            let db = 
                Database.Database(name + "cache", parser, Log.loggerInstance)
            
            Cache {new ICacheProvider with 
                            member __.InsertOrUpdate doc = 
                                async{
                                    doc.JsonValue.ToString()
                                    |> db.InsertOrUpdate 
                                    |> Log.logf "Inserted data: %s"
                                } |> Async.Start
                            
                            member __.Get (key : string) = 
                                Log.logf "trying to retrieve cached %s from database" key
                                key
                                |> db.TryGet }
        new(service : Http.CacheService -> Http.Service) =
            Cache {new ICacheProvider with 
                            member __.InsertOrUpdate doc = 
                                async{
                                    doc.JsonValue.ToString() 
                                    |> Http.put (Http.Update |> service) 
                                    |> ignore
                                } |> Async.Start
                            
                            member __.Get (key : string) = 
                                match Http.get (key |> Http.CacheService.Read |> service) parser with
                                Http.Success d -> Some d
                                | Http.Error (code,msg) ->
                                    Log.errorf null "Failed to load from cache. Status: %d. Message: %s" code msg
                                    None}
                                    
        member __.InsertOrUpdate = provider.InsertOrUpdate
        member __.Get = provider.Get