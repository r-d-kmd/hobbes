namespace Hobbes.Web

open Hobbes.Helpers.Environment
open Hobbes.Helpers
open Newtonsoft.Json

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
    
    type CacheRecord = 
        {
            [<JsonProperty("_id")>]
            CacheKey : string
            [<JsonProperty("timestamp")>]
            TimeStamp : System.DateTime option
            DependsOn : string list
            [<JsonProperty("data")>]
            Data : DataResult
        }
    type DynamicRecord = FSharp.Data.JsonProvider<"""{
        "_id" : "khjkjhkjh",
        "timestamp" : "13/07/2020 11:55:21",
        "dependsOn" : ["lkjlk","lhkjh"],
        "data" : [{"columnName1":"value","columnName2" : 1},{"columnName1":"value","columnName2" : 1}]
    }""">
    
    type BaseRecord = FSharp.Data.JsonProvider<"""{
        "_id" : "khjkjhkjh",
        "dependsOn" : ["lkjlk","lhkjh"]
    }""">

    let key (source : string) = 
        let whitespaceToRemove = [|' ';'\t';'\n';'\r'|]
        source.Split(whitespaceToRemove,System.StringSplitOptions.RemoveEmptyEntries)
        |> System.String.Concat
        |> hash
    
    let inline createCacheRecord key dependsOn (data : DataResult)  =
        let timeStamp = System.DateTime.Now
        {
            CacheKey =  key
            TimeStamp = Some timeStamp
            DependsOn = dependsOn
            Data = data
        }

    let createDynamicCacheRecord key (dependsOn : string list) (data : FSharp.Data.JsonValue []) =
        let dependsOn = System.String.Join(",", dependsOn)
        let data = 
            System.String.Join(",", data |> Array.map(fun d -> d.ToString()))
        let timeStamp = System.DateTime.Now
        sprintf """{
                    "_id": "%s",
                    "timestamp": "%A",
                    "dependsOn" : "[%s]",
                    "data": [%s]
                }""" key timeStamp dependsOn data
        |> DynamicRecord.Parse

    let readData (cacheRecordText : string) =
        let cacheRecord = Json.deserialize<CacheRecord> cacheRecordText 
        let data = cacheRecord.Data
        let columnNames = data.ColumnNames
        
        data.Rows()
        |> Seq.mapi(fun index row ->
            index,(row
                   |> Seq.zip columnNames)
        )

    type Cache<'recordType,'dataType> (name : string, deserializer : string -> 'recordType,serializer : 'recordType -> string , recordCreator : string -> string list -> 'dataType -> 'recordType) =
        let dbName = name + "cache"
        let db = 
            Database.Database(dbName, deserializer, Log.loggerInstance)
        let list = 
            Database.Database(dbName, BaseRecord.Parse, Log.loggerInstance)
            
        member __.InsertOrUpdate key dependsOn data = 
            let serialized = 
                data
                |> recordCreator key dependsOn
                |> serializer
            assert(try serialized |> deserializer |> ignore; true with _ -> false)
            serialized
            |> db.InsertOrUpdate 
            |> Log.debugf "Inserted data: %s"
        
        member __.Get (key : string) = 
            Log.logf "trying to retrieve cached %s from database" key
            key
            |> db.TryGet 
        member __.Peek (key : string) =
            db.TryGetRev key |> Option.isSome
        member cache.Delete (key : string) = 
            Log.logf "Deleting %s" key
            let sc,body= 
                key
                |> db.Delete
            list.List()
            |> Seq.filter(fun doc ->
                doc.DependsOn |> Array.contains key
            ) |> Seq.iter(fun doc -> cache.Delete doc.Id)
            if sc <> 200 then
                failwithf "Status code %d - %s" sc body
    let cache name = 
        Cache(name,
              Json.deserialize<CacheRecord>,
              Json.serialize,
              createCacheRecord)
    let dynamicCache name = 
        Cache(
            name,
            DynamicRecord.Parse,
            (fun dynRec -> dynRec.JsonValue.ToString()),
            createDynamicCacheRecord
        )