open FSharp.Control.Tasks.V2.ContextInsensitive
open Saturn
open Giraffe
open Microsoft.AspNetCore.Http
open Database
open Hobbes.Server.Security
open System

let private port = 
    match env "port" with
    null -> 8085
    | p -> int p
      
let private verified f =
    fun func (ctx : HttpContext) ->
        let authToken = 
            let url = ctx.GetRequestUrl()
            printfn "Requesting access to %s" url
            match Uri(url) with
            uri when String.IsNullOrWhiteSpace(uri.UserInfo) |> not ->
                uri.UserInfo |> Some
            | _ -> 
                ctx.TryGetRequestHeader "Authorization"
                
        let statusCode, body =  
            match authToken with
            None ->    
                    eprintfn "Tried to gain access without a key"
                    403, "Unauthorized"
            | Some authToken ->
                if authToken |> verifyAuthToken then
                    f()
                else 
                    403, "Unauthorized"
        (setStatusCode statusCode
         >=> setBodyFromString body) func ctx

let private handleRequestWithArg shouldVerify f arg : HttpHandler =
    fun next (ctx : HttpContext) ->
        task {
            if shouldVerify then
                return! verified (fun _ -> f arg) next ctx
            else 
                let code, body = f arg
                return! (setStatusCode code >=> setBodyFromString body) next ctx
        }

let private handleRequestWithBody shouldVerify f : HttpHandler =
    fun next (ctx : HttpContext) ->
        task {
            let! body = ctx.ReadBodyFromRequestAsync()
            return! handleRequestWithArg shouldVerify f body next ctx
        }

let private handleRequestWithBodyAndArg shouldVerify f arg =
    fun next (ctx : HttpContext) -> handleRequestWithBody shouldVerify (f arg) next ctx       

let private apiRouter = router {
    not_found_handler (setStatusCode 404 >=> text "Api 404")
    
    get "/ping" (handleRequestWithArg false Implementation.ping ())
    get "/init" (handleRequestWithArg false Implementation.initDb ())
    getf "/key/%s" (handleRequestWithArg false Implementation.key)
    getf "/csv/%s" (handleRequestWithArg true Implementation.csv)
    getf "/sync/%s" (handleRequestWithBodyAndArg true Implementation.sync)
    put "/configurations" (handleRequestWithBody true Implementation.storeConfigurations)
    get "/configurations" (handleRequestWithArg true Implementation.listConfigurations ())
    put "/transformations" (handleRequestWithBody true Implementation.storeTransformations)
    get "/transformations" (handleRequestWithArg true Implementation.listTransformations ())
    get "/cache" (handleRequestWithArg true Implementation.listCache ())
    getf "/status/sync/%s" (handleRequestWithArg true Implementation.getSyncState)
}

let private appRouter = router {
    forward "" apiRouter
}

let private app = application {
    url (sprintf "http://0.0.0.0:%d/" port)
    use_router appRouter
    memory_cache
    use_gzip
}

async {
    Implementation.initDb() |> ignore
} |> Async.Start

run app