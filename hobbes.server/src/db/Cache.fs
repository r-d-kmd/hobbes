namespace Hobbes.Server.Db 
open Hobbes.Server.Db.DataConfiguration
open FSharp.Data
open Hobbes.Web
open Hobbes.Server.Db
open Hobbes.Web
open Hobbes.Web.Log
open Hobbes.Shared.RawdataTypes

module Cache = 

    type SyncStatus = 
        Synced
        | Started
        | Failed
        | Updated
        | NotStarted
        with override x.ToString() = 
                match x with
                Synced -> "synced"
                | NotStarted -> "not started"
                | Started -> "started"
                | Updated -> "updated"
                | Failed -> "failed"
             static member Parse (s:string) =
                    match s.ToLower() with
                    "synced" -> Synced
                    | "started" -> Started
                    | "failed" -> Failed
                    | "updated" -> Updated
                    | "not started" -> NotStarted
                    | _ -> 
                        Log.debug (sprintf "Unknown sync state: %s" s)
                        Failed

      
    type DataValues =
        Floats of (int * float) []
        | Texts of (int * string) []
        | DateTimes of (int * System.DateTime) []
        with member x.Length 
               with get() = 
                   match x with
                   Floats a -> a.Length
                   | Texts a -> a.Length
                   | DateTimes a -> a.Length
             member x.Append other =
                match x,other with
                Floats a1, Floats a2 -> a2 |> Array.append a1 |> Floats
                | Texts a1, Texts a2 -> a2 |> Array.append a1 |> Texts
                | DateTimes a1,DateTimes a2 -> a2 |> Array.append a1 |> DateTimes
                | _ -> failwithf "Incompatible types: %A %A" x other
             member x.ToSeq() =
                match x with
                Floats a -> 
                    a |> Array.map(fun (i,v) -> i, box v)
                | Texts a ->
                    a |> Array.map(fun (i,v) -> i, box v)
                | DateTimes a ->
                    a |> Array.map(fun (i,v) -> i, box v)

    type DataRecord = {
        Columns : string []
        Values : DataValues []
    }

    type private CachedDate = JsonProvider<""" {"columnNames" : ["a","b"], "values" : [[0,1,2,3,4],[0.4,1.2,2.4,3.5,4.1],["x","y","z"],["2019-01.01","2019-01.01"]]} """>
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module TableView =
        let toTable (tableView : CachedDate.Root list) =
            tableView
            |> List.fold(fun (count, (map : Map<_,_>)) record ->
                let values = 
                    record.Values
                    |> Array.map(fun raw -> 
                       match raw.Numbers with
                       [||] -> 
                           match raw.Strings with
                           [||] -> 
                               raw.DateTimes
                               |> Array.mapi (fun i dt -> i + count,dt)
                               |> DateTimes
                           | strings ->
                               strings
                               |> Array.mapi (fun i dt -> i + count,dt)
                               |> Texts
                       | numbers ->
                           numbers
                           |> Array.mapi(fun i n -> i + count, float n)
                           |> Floats
                    )
                let map = 
                   record.ColumnNames
                   |> Array.indexed
                   |> Array.fold(fun map (i,columnName) ->
                       assert(values.Length > i)
                       let columnValues = values.[i]
                       match map |> Map.tryFind columnName with
                       None -> map.Add(columnName, columnValues)
                       | Some vs -> map.Add(columnName, vs.Append columnValues)
                   ) map
                //Values can have empty cells in the end but needs to be aligned on the first element
                let maxLength = 
                    (values
                     |> Array.maxBy(fun a -> a.Length)).Length
                count + maxLength, map
            ) (0,Map.empty)
            |> snd
            |> Map.toSeq
        let parse s = 
            CachedDate.Parse s
    let private sourceView = "srcproj"
    let private db = 
        Database.Database("cache", CacheRecord.Parse, Log.loggerInstance) 
          .AddView(sourceView)

    let private createKeyList (configuration : Configuration) =
        let searchKey = configuration.SearchKey
        let transformations = configuration.Transformations
        searchKey::(transformations |> List.ofArray)

    let private createKeyFromlist (keyList : #seq<string>) = 
        System.String.Join(":",keyList).ToLower()

    let private createKey configuration = 
        createKeyList configuration
        |> createKeyFromlist
        
    let InsertOrUpdate doc = 
        db.InsertOrUpdate doc
        
    let list() = 
        db.ListIds()

    let createDataRecord key searchKey (data : string) keyValue =
        
        let data = if isNull data then data else data.Replace("\\", "\\\\")
        let record = 
            (sprintf """{
                        "_id" : "%s",
                        "searchKey" : "%s",
                        "timeStamp" : "%s"
                        %s%s
                    }""" key
                         searchKey
                         (System.DateTime.Now.ToString (System.Globalization.CultureInfo.CurrentCulture)) 
                          
                         (if data |> isNull then 
                              "" 
                          else 
                              sprintf """, "data": %s""" data)
                          (match keyValue with
                          [] -> ""
                          | values ->
                              System.String.Join(",",
                                  values
                                  |> Seq.map(fun (k,v) -> sprintf """%A:%A""" k v)
                              ) |> sprintf """,%s"""
                         ))
        let parsedRecord = record |> CacheRecord.Parse
        
        assert(parsedRecord.SearchKey = searchKey)
        assert(parsedRecord.Id = key)

        record

    let createCacheRecord key searchKey (data : string) (state : SyncStatus) message cacheRevision =
        let values = 
            [
               if cacheRevision |> Option.isSome then yield "revision", string cacheRevision.Value
               yield "state", string state
               if message |> Option.isSome then yield "message", message.Value
            ]

        createDataRecord key searchKey data values

    let store transformations searchKey cacheRevision (data : string) =
        if transformations |> List.isEmpty then failwith "Won't cache data that's not transformed"
        let key = searchKey::transformations |> createKeyFromlist
        let record = createCacheRecord key searchKey data Synced None (Some cacheRevision)

        try
            db.InsertOrUpdate record |> ignore
        with e ->
            Log.errorf e.StackTrace "Failed to cache data. Reason: %s" e.Message
            Log.debug data
        (CacheRecord.Parse record).Data.ToString()

    let private tryRetrieve cacheKey =
        cacheKey
        |> createKeyFromlist
        |> db.TryGet 
        |> Option.bind(fun (cacheRecord : CacheRecord.Root) -> 
            [
                cacheRecord.Data.ToString()
                |> TableView.parse
            ] |> TableView.toTable
            |> Seq.map(fun (columnName,values) -> 
                columnName, values.ToSeq()
                            |> Seq.map(fun (i,v) -> i, v)
            ) |> Some 
        )

    let delete =
        db.Delete

    let clear()=
        //todo: make a bulk update instead setting the prop _deleted to true
        async {
            let!_ =
                db.ListIds()
                |> Seq.filter(fun id ->
                   (id.StartsWith "_design" || id = "default_hash") |> not
                )
                |> Seq.map(fun id -> 
                    async { 
                        let status,body = delete id
                        if status > 299 then
                            Log.errorf "" "Couldn't delete cache %s. Messahe: %s" id body
                        else
                            Log.debugf "Deleted cache %s" id
                    }
                ) |> Async.Parallel
            return ()
        } |> Async.Start
        200,"deleting"

    let findUncachedTransformationsAndCachedData configuration =
        async{
            let rec find key =
                match key with
                [] | [_] -> 
                    key, None
                | source::project::transformations as keys->
                   match keys |> tryRetrieve with
                   None -> 
                       match transformations with
                       [] -> key,None
                       | transformations -> source::project::(transformations |> List.rev |> List.tail |> List.rev) |> find
                   | data -> keys, data
            return 
                match find (createKeyList configuration) with
                [],_ | [_], _ | [_;_], None -> configuration.Transformations |> Array.toList, None
                | _::_::transformations, data ->
                    transformations
                    |> List.filter(fun transformation ->
                        transformations
                        |> List.tryFind(fun t -> t = transformation)
                        |> Option.isNone
                    ),data
        }
            
    let private idsBySource (config : Configuration) =
        let startKey = config.SearchKey
        
        db.Views.[sourceView].List(CacheRecord.Parse, 
                                      startKey =  startKey)

    let invalidateCache config cacheRevision =
        async {
            let! _ = 
                idsBySource config 
                |> List.map(fun doc -> 
                                async {
                                    if doc.Revision = cacheRevision then
                                        delete doc.Id |> ignore
                                } 
                ) |> Async.Parallel
            return ()
        }

    let retrieveRaw key = 
        db.Get [key]

    let retrieve (configuration : Configuration) =
       (
           configuration
           |> createKey
           |> db.Get
       ).Data.ToString()