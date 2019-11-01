open FSharp.Control.Tasks.V2.ContextInsensitive
open Saturn
open Giraffe
open Microsoft.AspNetCore.Http
open Hobbes.Server.Db.Database
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

let private sync configurationName =
    fun f (ctx : HttpContext) ->
        try
            match ctx.TryGetRequestHeader "PAT" with
            Some azurePAT ->
                verified (fun () -> Implementation.sync azurePAT configurationName) f ctx
            | None -> 
                (setStatusCode 403 >=> setBodyFromString "Unauthorized") f ctx
        with e -> 
            eprintfn "Couldn't sync %s. Reason: %s" configurationName e.Message
            (setStatusCode 500 >=> setBodyFromString e.Message) f ctx

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
    put "/transformations" (handleRequestWithBody true Implementation.storeTransformations)
    get "/list/configurations" (handleRequestWithArg true Implementation.listConfigurations ())
    get "/list/transformations" (handleRequestWithArg true Implementation.listTransformations ())
    get "/list/cache" (handleRequestWithArg true Implementation.listCache ())
    get "/list/rawdata" (handleRequestWithArg true Implementation.listRawdata ())
    getf "/status/sync/%s" (handleRequestWithArg true Implementation.getSyncState)

    getf "/admin/settings/%s/%s" (handleRequestWithArg true Implementation.setting)
    putf "/admin/configure/%s/%s/%s" (handleRequestWithArg true Implementation.configure)
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

let rec private init() =
    async {
        try
           Implementation.initDb() |> ignore
           printfn "DB initialized"
        with _ ->
           do! Async.Sleep 2000
           init()
    } |> Async.Start

init()
run app