open Saturn
open Giraffe
open Hobbes.Calculator.Services.Data
open Hobbes.Web
open Hobbes.Web.Routing
open Hobbes.Helpers.Environment
open Hobbes.Messaging.Broker
open Hobbes.Messaging
open Hobbes.Web.RawdataTypes

let private port = 8085

let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "The requested ressource does not exist")
    
    fetch <@ ping @>
    withArg <@ configuration @>
    withArg <@ transformation @>
    withArg <@ sources @>
    fetch <@ collectors @>
    withBody <@ storeConfiguration @>
    withBody <@ storeTransformation @>
} 

let private app = application {
    url "http://0.0.0.0:8085/"
    use_router appRouter
    memory_cache
    use_gzip
}
let peek cacheKey =
    match Http.get (cacheKey |> Http.UniformDataService.Read |> Http.UniformData) Hobbes.Helpers.Json.deserialize<Cache.CacheRecord> with
    Http.Error _ -> 
        false
    | Http.Success _ -> 
        true

type DependingTransformationList = FSharp.Data.JsonProvider<"""[
    {
        "_id" : "lkjlkj",
        "lines" : ["lkjlkj", "lkjlkj","o9uulkj"]
    }
]""">
let mutable time = System.DateTime.MinValue
let mutable dependencies : Map<string,seq<Transformation>> = Map.empty
let mutable merges : Map<string,Config.Root> = Map.empty
let mutable joins : Map<string,Config.Root> = Map.empty
let dependingTransformations (cacheKey : string) =
    assert(not(cacheKey.EndsWith(":") || System.String.IsNullOrWhiteSpace cacheKey))
#if DEBUG    
    let isCacheStale = true
#else
    let isCacheStale = (dependencies.Count = 0 || time < System.DateTime.Now.AddHours -1.)
#endif    
    if isCacheStale then
        time <- System.DateTime.Now
        dependencies <-
            configurations.List()
            |> Seq.collect(fun configuration ->
                let transformations = 
                    configuration.Transformations
                    |> Array.map(fun transformationName ->
                        match transformations.TryGet transformationName with
                        None -> 
                            Log.errorf  "Transformation (%s) not found" transformationName
                            None
                        | t -> t
                    ) |> Array.filter Option.isSome
                    |> Array.map Option.get
                    |> Array.toList

                match transformations with
                [] -> []
                | h::tail ->
                    tail
                    |> List.fold(fun (lst : (string * Transformation) list) t ->
                        let prevKey, prevT = lst |> List.head
                        (prevKey + ":" + prevT.Name,t) :: lst
                    ) [keyFromSource configuration.Source,h]
            ) |> Seq.groupBy fst
            |> Seq.map(fun (key,deps) ->
                key,
                    deps 
                    |> Seq.map snd 
                    |> Seq.distinctBy(fun t -> t.Name)
            ) |> Map.ofSeq
        merges <-
            configurations.List()
            |> Seq.filter(fun configuration ->
                configuration.Source.Provider = "merge"
            ) |> Seq.collect(fun configuration ->
                configuration.Source.Datasets
                |> Array.map(fun ds -> ds,configuration)
            ) |> Map.ofSeq
        joins  <-
            configurations.List()
            |> Seq.filter(fun configuration ->
                configuration.Source.Provider = "join"
            ) |> Seq.collect(fun configuration ->
                [
                    configuration.Source.Left.Value,configuration
                    configuration.Source.Right.Value,configuration
                ]
            ) |> Map.ofSeq
    match dependencies |> Map.tryFind cacheKey with
    None -> 
        Log.debugf "No dependencies found for key (%s)" cacheKey
        Seq.empty
    | Some dependencies ->
        dependencies
let handleMerges cacheKey = 
    match merges |> Map.tryFind cacheKey with
    None -> ()
    | Some configuration ->
        let allUpdated = 
            configuration.Source.Datasets
            |> Array.exists(peek)
            |> not
        if allUpdated then
            {
                CacheKey = configuration |> keyFromConfig
                Datasets = configuration.Source.Datasets
            } |> Merge
            |> Broker.Calculation

let handleJoins cacheKey = 
    match joins |> Map.tryFind cacheKey with
    None -> ()
    | Some configuration ->
        {
            CacheKey = configuration |> keyFromConfig
            Left = configuration.Source.Left.Value
            Right = configuration.Source.Right.Value
            Field = configuration.Source.Field.Value
        } |> Join
        |> Broker.Calculation
let getDependingTransformations (cacheMsg : CacheMessage) = 
    try
         match cacheMsg with
         CacheMessage.Empty -> Success
         | Updated cacheKey -> 
            dependingTransformations cacheKey
            |> Seq.iter(fun transformation ->    
                {
                    Transformation = 
                        {
                            Name = transformation.Name
                            Statements = transformation.Statements
                        }
                    DependsOn = cacheKey
                }
                |> Transform
                |> Broker.Calculation
            )
            handleMerges cacheKey
            handleJoins cacheKey
            Success
    with e ->
        Log.excf e "Failed to perform calculation."
        Excep e

[
   "configurations"
   "transformations"
] |> Database.initDatabases

async {    
    do! awaitQueue()
    Broker.Cache getDependingTransformations
} |> Async.Start

run app