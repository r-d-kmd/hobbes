module Cache
open Hobbes.Server.Db.DataConfiguration
let private createKeyFromList  (cacheKey : string list) =  
    System.String.Join(":",cacheKey) 

let private createKey (configuration : Configuration) = 
    configuration.Source.SourceName::configuration.Source.ProjectName::configuration.Transformations

let store configuration (data : string) =
    let cacheKey = 
        configuration 
        |> createKey 
        |> createKeyFromList
       
    let record = 
            sprintf """{
                "Source" : "%s",
                "Project" : "%s",
                "TimeStamp" : "%s",
                "Data" : %s
            }""" configuration.Source.SourceName
                 configuration.Source.ProjectName
                 (System.DateTime.Now.ToString (System.Globalization.CultureInfo.CurrentCulture)) 
                 (data.Replace("\\","\\\\"))

    try
        Database.cache.Put(cacheKey, record) |> ignore
    with e ->
        eprintfn "Failed to cache data. Reason: %s" e.Message
    (Database.CacheRecord.Parse record).Data.ToString()

let private tryRetrieve cacheKey =
    cacheKey
    |> createKeyFromList
    |> Database.cache.TryGet 
    |> Option.bind(fun (cacheRecord : Database.CacheRecord.Root) -> 
        [|
            cacheRecord.Data.ToString()
            |> Database.TableView.Parse
        |] |> Database.TableView.toTable
        |> Seq.map(fun (columnName,values) -> 
            columnName, values.ToSeq()
                        |> Seq.map(fun (i,v) -> Hobbes.Parsing.AST.KeyType.Create i, v)
        ) 
        |> Hobbes.FSharp.DataStructures.DataMatrix.fromTable
        |> Some
    )

let delete id =
    Database.cache.Delete id

let findUncachedTransformations configuration =
    let rec find key =
        match key with
        [] -> 
            [], None
        | keys ->
           match keys |> tryRetrieve with
           None -> keys |> List.tail |> find
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

let retrieve (configuration : Configuration) =
   (
       configuration
       |> createKey
       |> createKeyFromList
       |> Database.cache.Get
    ).Data.ToString()