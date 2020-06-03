namespace Hobbes.Web

open Hobbes.Helpers.Environment
open Hobbes.Helpers
open Newtonsoft.Json

module Cache =
    type Value =
        String of string
        | Int of int
        | Float of float
        | Date of System.DateTime
    type Row =
        Element of Value
        | Row of Value * Row
        with 
            member x.ToList() =
              match x with
              Element v -> [v]
              | Row(v,tail) -> v::(tail.ToList())
            member x.ToArray() =
                x.ToList() |> List.toArray

    type DataResult = 
        {
            [<JsonProperty("columnNames")>]
            ColumnNames : string []
            [<JsonProperty("rows")>]
            Rows : Row []
            [<JsonProperty("rowCount")>]
            RowCount : int
        }
    
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

    let readData (cacheRecordText : string) =
        let cacheRecord = Json.deserialize<CacheRecord> cacheRecordText 
        let data = cacheRecord.Data
        let columnNames = data.ColumnNames
        
        data.Rows
        |> Seq.mapi(fun index row ->
            index,(row.ToArray()
                   |> Seq.zip columnNames)
        )

    type ICacheProvider = 
         abstract member InsertOrUpdate : string -> DataResult -> unit
         abstract member Get : string -> CacheRecord option

    type Cache private(provider : ICacheProvider) =
        static let parser = Json.deserialize<CacheRecord>
        new(name) =  
            let db = 
                Database.Database(name + "cache", parser, Log.loggerInstance)
            
            Cache {new ICacheProvider with 
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
            Cache {new ICacheProvider with 
                            member __.InsertOrUpdate key data = 
                                data
                                |> Json.serialize 
                                |> sprintf """["%s",%s]""" key
                                |> Http.post (Http.Update |> service) id 
                                |> ignore
                                
                            
                            member __.Get (key : string) = 
                                match Http.get (key |> Http.CacheService.Read |> service) parser with
                                Http.Success d -> Some d
                                | Http.Error (code,msg) ->
                                    Log.errorf  "Failed to load from cache. Status: %d. Message: %s" code msg
                                    None}
                                    
        member __.InsertOrUpdate = provider.InsertOrUpdate
        member __.Get = provider.Get