open Saturn
open Giraffe
open Hobbes.Calculator.Services.Data
open Hobbes.Web
open Hobbes.Web.Routing
open Hobbes.Helpers.Environment
open Hobbes.Messaging.Broker
open Hobbes.Messaging
open Hobbes.Web.RawdataTypes

let private port = env "PORT" "8085"
                   |> int

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
    url (sprintf "http://0.0.0.0:%d/" port)
    use_router appRouter
    memory_cache
    use_gzip
}
let cache = Cache.Cache(Http.UniformData)
type DependingTransformationList = FSharp.Data.JsonProvider<"""[
    {
        "_id" : "lkjlkj",
        "lines" : ["lkjlkj", "lkjlkj","o9uulkj"]
    }
]""">
let mutable (time,dependencies : Map<string,seq<Transformation>>) = (System.DateTime.MinValue,Map.empty)
    
let dependingTransformations (cacheKey : string) =
    assert(not(cacheKey.EndsWith(":") || System.String.IsNullOrWhiteSpace cacheKey))
#if DEBUG    
    let isCacheStale = true
#else
    let isCacheStale = time < System.DateTime.Now.AddHours -1. 
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
                    ) [keyFromConfig configuration,h]
            ) |> Seq.groupBy fst
            |> Seq.map(fun (key,deps) ->
                key,
                    deps 
                    |> Seq.map snd 
                    |> Seq.distinctBy(fun t -> t.Name)
            ) |> Map.ofSeq
    match dependencies |> Map.tryFind cacheKey with
    None -> 
        Log.debugf "No dependencies found for key (%s)" cacheKey
        Seq.empty
    | Some dependencies ->
        dependencies
        
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
                    CacheKey = cacheKey
                }
                |> Transform
                |> Broker.Calculation
            )
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