namespace Hobbes.Web

open Hobbes.Helpers.Environment
open Hobbes.Helpers
open Newtonsoft.Json
open FSharp.Data

module Cache =
    [<RequireQualifiedAccess>]
    type Value = 
       Int of int
       | Float of float
       | Date of System.DateTime
       | Text of string
       | Boolean of bool
       | Null
       with static member Create v =
                match v :> obj with        
                null -> Value.Null
                | :? System.DateTime as d -> 
                    d |> Value.Date
                | :? System.DateTimeOffset as d -> 
                    d.ToLocalTime().DateTime |> Value.Date
                | :? int as i  -> i |> Value.Int
                | :? float as f -> f |> Value.Float
                | :? decimal as d -> d |> float |> Value.Float
                | :? string as s  -> s |> Value.Text
                | s ->
                    Log.errorf "Didn't recognise value (%A)" s
                    assert(false)
                    s |> string |> Value.Text
            static member Bind v = 
                match v with
                Some v -> 
                    Value.Create v
                | None -> 
                    Value.Create null

    type DataResult = 
        {
            [<JsonProperty("columnNames")>]
            ColumnNames : string []
            [<JsonProperty("rows")>]
            Values : Value [][]
            [<JsonProperty("rowCount")>]
            RowCount : int
        } with member x.Rows() =
                    x.Values
                    |> Array.map(
                           Array.map(
                               function
                                   Value.Int i -> box i
                                   | Value.Float f -> box f
                                   | Value.Date d -> box d
                                   | Value.Text s -> box s
                                   | Value.Null -> null
                                   | Value.Boolean b -> box b
                           )
                    )

    type DynamicCacheRecord = JsonProvider<"""
        {
            "_id" : "Put1DHere",
            "timestamp" : "this is a time stamp",
            "data" : []
        }
    """>
    
    type CacheRecord = 
        {
            [<JsonProperty("_id")>]
            CacheKey : string
            [<JsonProperty("timestamp")>]
            TimeStamp : System.DateTime option
            [<JsonProperty("data")>]
            Data : DataResult
        }

    let key (source : string) = 
        let whitespaceToRemove = [|' ';'\t';'\n';'\r'|]
        source.Split(whitespaceToRemove,System.StringSplitOptions.RemoveEmptyEntries)
        |> System.String.Concat
        |> hash
    
      
    let private createCacheRecord key (data : DataResult) =

        let timeStamp = System.DateTime.Now
        {
            CacheKey =  key
            TimeStamp = Some timeStamp
            Data = data
        }

    let private createDynamicCacheRecord key data =

        let timeStamp = System.DateTime.Now
        sprintf """{
                    "_id": "%s",
                    "timestamp": "%A",
                    "data": %s
                }""" key timeStamp data

    let readData (cacheRecordText : string) =
        let cacheRecord = Json.deserialize<CacheRecord> cacheRecordText 
        let data = cacheRecord.Data
        let columnNames = data.ColumnNames
        
        data.Rows()
        |> Seq.mapi(fun index row ->
            index,(row
                   |> Seq.zip columnNames)
        )

    type ICacheProviderDataResult = 
         abstract member InsertOrUpdate : string -> DataResult -> unit
         abstract member Get : string -> CacheRecord option

    type ICacheProviderGeneric = 
         abstract member InsertOrUpdate : string -> string -> unit
         abstract member Get : string -> DynamicCacheRecord.Root option

    type DataResultCache private(provider : ICacheProviderDataResult) =
        static let parser = Json.deserialize<CacheRecord>
        new(name) =  
            let db = 
                Database.Database(name + "cache", parser, Log.loggerInstance)
            
            DataResultCache {new ICacheProviderDataResult with 
                            member __.InsertOrUpdate key data = 
                                    data
                                    |> createCacheRecord key 
                                    |> db.InsertOrUpdate 
                                    |> Log.debugf "Inserted data: %s"
                            
                            member __.Get (key : string) = 
                                Log.logf "trying to retrieve cached %s from database" key
                                key
                                |> db.TryGet }
        new(service : Http.CacheService -> Http.Service) =
            DataResultCache {new ICacheProviderDataResult with 
                            member __.InsertOrUpdate key data = 
                                data
                                |> createCacheRecord key 
                                |> Http.post (Http.Update |> service)
                                |> ignore
                                
                            member __.Get (key : string) = 
                                match Http.get (key |> Http.CacheService.Read |> service) parser with
                                Http.Success d -> Some d
                                | Http.Error (code,msg) ->
                                    Log.errorf  "Failed to load from cache. Status: %d. Message: %s" code msg
                                    None}
                                    
        member __.InsertOrUpdate = provider.InsertOrUpdate
        member __.Get = provider.Get

    type GenericCache private(provider : ICacheProviderGeneric) =
        static let parser = DynamicCacheRecord.Parse     
        new(name) =  
            let db = 
                Database.Database(name + "cache", parser, Log.loggerInstance)
            
            GenericCache {new ICacheProviderGeneric with 
                            member __.InsertOrUpdate key data = 
                                    data 
                                    |> createDynamicCacheRecord key
                                    |> db.InsertOrUpdate
                                    |> Log.debugf "Inserted data: %s"
                            
                            member __.Get (key : string) = 
                                Log.logf "trying to retrieve cached %s from database" key
                                key
                                |> db.TryGet }

        new(service : Http.CacheService -> Http.Service) =
            GenericCache {new ICacheProviderGeneric with 
                            member __.InsertOrUpdate key data =
                                data 
                                |> createDynamicCacheRecord key
                                |> Http.post (Http.Update |> service)
                                |> ignore
                                
                            member __.Get (key : string) = 
                                match Http.get (key |> Http.CacheService.Read |> service) parser with
                                Http.Success d -> Some d
                                | Http.Error (code,msg) ->
                                    Log.errorf  "Failed to load from cache. Status: %d. Message: %s" code msg
                                    None}

        member __.InsertOrUpdate = provider.InsertOrUpdate
        member __.Get = provider.Get