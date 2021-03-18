open Hobbes.Helpers.Environment
open Hobbes.Web
open Hobbes.Messaging
open Hobbes.Messaging.Broker
open FSharp.Data
open Saturn
open Giraffe
open Hobbes.Web.Routing
open LocalData.Data


let handleMessage message =
    match message with
    Empty -> Success
    | Sync sourceDoc -> 
        Log.debugf "Received message. %s" sourceDoc
        try
            let source = sourceDoc |> LocalDataProviderConfig.Parse
            let key,data = synchronize source
            let data = Cache.createCacheRecord key [] data
            match Http.post (Http.UniformData Http.Update) (data.ToString()) with
            Http.Success _ -> 
               Log.logf "Data uploaded to cache"
               Success
            | Http.Error(status,msg) -> 
                sprintf "Upload of %s to uniform data failed. %d %s" (data.ToString()) status msg
                |> Failure
        with e ->
            Log.excf e "Failed to process message"
            Excep e

Database.awaitDbServer()
async{    
    do! awaitQueue()
    Broker.LocalData handleMessage
} |> Async.RunSynchronously


let private port = 8085

let private appRouter = router {
    not_found_handler (setStatusCode 404 >=> text "The requested ressource does not exist")
    
    withBody <@ loadConfig @>
} 

let private app = application {
    url "http://0.0.0.0:8085/"
    use_router appRouter
    memory_cache
    use_gzip
}

run app