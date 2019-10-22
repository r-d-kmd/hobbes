module Cache
open Hobbes.Server.Db.DataConfiguration
open FSharp.Data

type CacheRecord = JsonProvider<"""{
    "_id" : "name",
    "timeStamp" : "24-09-2019",
    "source" : "lækljk",
    "project" : "lkjlkj",
    "state" : "Sync state",
    "data" : {
        "columnNames" : ["a","b"],
        "values" : [["zcv"],[1.2],["2019-01-01"]]
    }
}""">

type SyncStatus = 
    Synced
    | Started
    | Failed
    | NotStarted
    with override x.ToString() = 
            match x with
            Synced -> "synced"
            | NotStarted -> "not started"
            | Started -> "started"
            | Failed -> "failed"
         static member Parse (s:string) =
                match s.ToLower() with
                "synced" -> Synced
                | "started" -> Started
                | "failed" -> Failed
                | "not started" -> NotStarted
                | _ -> 
                    eprintfn "Unknown sync state: %s" s
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

type private TableView = JsonProvider<""" {"columnNames" : ["a","b"], "values" : [[0,1,2,3,4],[0.4,1.2,2.4,3.5,4.1],["x","y","z"],["2019-01.01","2019-01.01"]]} """>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TableView =
    let toTable (tableView : TableView.Root []) =
        tableView
        |> Array.fold(fun (count, (map : Map<_,_>)) record ->
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
        TableView.Parse s
let private sourceView = "srcproj"
let private db = 
    Database.Database("cache", CacheRecord.Parse)
      .AddView(sourceView)

let private createKeyFromList  (cacheKey : string list) =  
    System.String.Join(":",cacheKey) 

let private createKey (configuration : Configuration) = 
    configuration.Source.SourceName::configuration.Source.ProjectName::configuration.Transformations

let InsertOrUpdate doc = 
    db.InsertOrUpdate doc
    
let list() = 
    db.ListIds()

let createDataRecord key (source : DataSource) (data : string) keyValue =
    let record = 
        sprintf """{
                    "_id" : "%s",
                    "source" : "%s",
                    "project" : "%s",
                    "timeStamp" : "%s",
                    "data" : %s%s
                }""" key
                     source.SourceName
                     source.ProjectName
                     (System.DateTime.Now.ToString (System.Globalization.CultureInfo.CurrentCulture)) 
                     (if data |> isNull then "null" else data.Replace("\\","\\\\"))
                     (match keyValue with
                      [] -> ""
                      | values ->
                          System.String.Join(",",
                              values
                              |> Seq.map(fun (k,v) -> sprintf """%A:%A""" k v)
                          )
                          |> sprintf """,%s"""
                     )
    let parsedRecord = record |> CacheRecord.Parse
    //validate that the model fits expectations
    assert(parsedRecord.Id = key)
    assert(parsedRecord.Source = source.SourceName)
    assert(parsedRecord.Project = source.ProjectName)
    record

let createCacheRecord configuration (data : string) (state : SyncStatus) message =
    let cacheKey = 
        configuration 
        |> createKey 
        |> createKeyFromList

    createDataRecord cacheKey configuration.Source data [
                                                           yield "state", string state
                                                           if message |> Option.isSome then yield "message", message.Value]

let store configuration (data : string) =

    let record = createCacheRecord configuration data Synced None

    try
        db.InsertOrUpdate record |> ignore
    with e ->
        eprintfn "Failed to cache data. Reason: %s" e.Message
    (CacheRecord.Parse record).Data.ToString()

let private tryRetrieve cacheKey =
    cacheKey
    |> createKeyFromList
    |> db.TryGet 
    |> Option.bind(fun (cacheRecord : CacheRecord.Root) -> 
        [|
            cacheRecord.Data.ToString()
            |> TableView.Parse
        |] |> TableView.toTable
        |> Seq.map(fun (columnName,values) -> 
            columnName, values.ToSeq()
                        |> Seq.map(fun (i,v) -> Hobbes.Parsing.AST.KeyType.Create i, v)
        ) 
        |> Hobbes.FSharp.DataStructures.DataMatrix.fromTable
        |> Some
    )

let delete id =
    db.Delete id

let findUncachedTransformations configuration =
    let rec find key =
        match key with
        [] | [_]-> 
            key, None
        | source::project::transformations as keys->
           match keys |> tryRetrieve with
           None -> 
               match transformations with
               [] -> key,None
               | transformations -> source::project::(transformations |> List.rev |> List.tail |> List.rev) |> find
           | data -> keys, data
    match find (configuration |> createKey) with
    [],_ | [_], _ | [_;_], None -> configuration.Transformations, None
    | _::_::transformations, data ->
        configuration.Transformations
        |> List.filter(fun transformation ->
            transformations
            |> List.tryFind(fun t -> t = transformation)
            |> Option.isNone
        ),data
        
let private idsBySource (source : DataSource) =
    let startKey = 
        sprintf """["%s","%s"]""" source.SourceName source.ProjectName
    let endKey = 
        sprintf """["%s","%s_"]""" source.SourceName source.ProjectName
    db.Views.[sourceView].List(Database.CouchDoc.Parse, 
                                  startKey =  startKey, 
                                  endKey = endKey)

let invalidateCache (source : DataSource) =
    async {
        let! _ = 
            idsBySource source
            |> Array.map(fun doc -> 
                            async {
                                delete doc.Id |> ignore
                            } 
            ) |> Async.Parallel
        return ()
    }

let retrieve (configuration : Configuration) =
   (
       configuration
       |> createKey
       |> createKeyFromList
       |> db.Get
   ).Data.ToString()

let tryGetRev id = db.TryGetRev id
let tryGetHash id = db.TryGetHash id