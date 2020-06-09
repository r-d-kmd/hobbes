open Saturn
open Giraffe
open Hobbes.Calculator.Services.Data
open Hobbes.Web
open Hobbes.Web.Routing
open Hobbes.Helpers.Environment
open Hobbes.Messaging.Broker
open Hobbes.Messaging

let private port = env "PORT" "8085"
                   |> int

let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "The requested ressource does not exist")
    
    fetch <@ ping @>
    withArg <@ configuration @>
    withArg <@ transformation @>
    withArg <@ dependingTransformations @>
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
let getDependingTransformations (cacheMsg : CacheMessage) = 
    try
         match cacheMsg with
         CacheMessage.Empty -> Success
         | Updated cacheKey -> 
            match cache.Get cacheKey with
            None -> 
                Log.logf "No data for that key (%s)" cacheKey
                Success
            | Some cacheRecord -> 
                let service = cacheKey |> Http.DependingTransformations |> Http.Configurations
                match Http.get service DependingTransformationList.Parse  with
                Http.Success transformations ->
                    transformations
                    |> Seq.iter(fun transformation ->    
                        {
                            Transformation = 
                                {
                                    Name = transformation.Id
                                    Statements = transformation.Lines
                                }
                            CacheKey = cacheKey
                        }
                        |> Transform
                        |> Broker.Calculation
                    )
                    Success
                | Http.Error(404,_) ->
                    Log.debug "No depending transformations found."
                    Success
                | Http.Error(sc,m) ->
                    sprintf "Failed to get list of depending transformations data (%s) %d %s" cacheKey sc m
                    |> Failure 
    with e ->
        Log.excf e "Failed to perform calculation."
        Excep e

[
   "configurations"
   "transformations"
] |> Hobbes.Web.Database.initDatabases

async {    
    do! awaitQueue()
    Broker.Cache getDependingTransformations
} |> Async.Start

run app