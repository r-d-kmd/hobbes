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

    let readData (cacheRecordText : string) =
        let cacheRecord = Json.deserialize<CacheRecord> cacheRecordText 
        let data = cacheRecord.Data
        let columnNames = data.ColumnNames
        
        data.Rows()
        |> Seq.mapi(fun index row ->
            index,(row
                   |> Seq.zip columnNames)
        )

    type ICacheProvider = 
         abstract member InsertOrUpdate : string -> string list -> DataResult -> unit
         abstract member Get : string -> CacheRecord option
         abstract member Delete : string -> unit
         abstract member Peek : string -> bool

    type Cache private(provider : ICacheProvider) =
        static let parser = Json.deserialize<CacheRecord>
        new(name) =  
            let db = 
                Database.Database(name + "cache", parser, Log.loggerInstance)
            
            Cache {new ICacheProvider with 
                            member __.InsertOrUpdate key dependsOn data = 
                                    data
                                    |> createCacheRecord key dependsOn
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
                                db.List()
                                |> Seq.filter(fun doc ->
                                    doc.DependsOn |> List.contains key
                                ) |> Seq.iter(fun doc -> cache.Delete doc.CacheKey)
                                if sc <> 200 then
                                    failwithf "Status code %d - %s" sc body
                            }
        new(service : Http.CacheService -> Http.Service) =
            Cache {new ICacheProvider with 
                            member __.InsertOrUpdate key dependsOn data = 
                                data
                                |> createCacheRecord key dependsOn
                                |> Http.post (Http.Update |> service)
                                |> ignore
                            member __.Get (key : string) = 
                                match Http.get (key |> Http.CacheService.Read |> service) parser with
                                Http.Success d -> Some d
                                | Http.Error (code,msg) ->
                                    Log.errorf  "Failed to load from cache. Status: %d. Message: %s" code msg
                                    None
                            member this.Peek (key : string) = this.Get key |> Option.isSome
                            member __.Delete (key : string) = 
                                match Http.get (key |> Http.CacheService.Delete |> service) parser with
                                Http.Success _ -> ()
                                | Http.Error (code,msg) ->
                                    failwithf  "Failed to delete from cache. Status: %d. Message: %s" code msg
                            }
                            
        interface ICacheProvider with                         
            member __.InsertOrUpdate key dependsOn data = provider.InsertOrUpdate key dependsOn data
            member __.Get key = provider.Get key
            member __.Delete key = provider.Delete key
            member __.Peek key = provider.Peek key