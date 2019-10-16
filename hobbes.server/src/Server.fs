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

let private data configurationName =
    verified (fun _ -> Implementation.data configurationName)

let private sync configurationName =
    fun f (ctx : HttpContext) ->
        try
            match ctx.TryGetRequestHeader "PAT" with
            Some pat ->
                verified (fun () -> Implementation.sync pat configurationName) f ctx
            | None -> 
                (setStatusCode 403 >=> setBodyFromString "Unauthorized") f ctx
        with e -> 
            eprintfn "Couldn't sync %s. Reason: %s" configurationName e.Message
            (setStatusCode 500 >=> setBodyFromString e.Message) f ctx

let private putDocument (handler : string -> int * string) : HttpHandler =
    fun next (ctx : HttpContext) ->
        task {
            let! body = ctx.ReadBodyFromRequestAsync()
            return! verified (fun _ -> handler body) next ctx
        }

let private initDb : HttpHandler =
    fun next ctx ->
        task {
          let sc = Implementation.initDb()
          return! (setStatusCode sc >=> setBodyFromString "") next ctx
        }

let private key token =
    let statusCode,body = Implementation.key token
    setStatusCode statusCode >=> setBodyFromString body

let private ping : HttpHandler =
    fun next ctx -> 
        task { 
            let statusCode,body = Implementation.ping()
            return! (setStatusCode statusCode >=> setBodyFromString body) next ctx
        }

let private apiRouter = router {
    not_found_handler (setStatusCode 404 >=> text "Api 404")
    
    getf "/data/%s" data
    getf "/key/%s" key
    get "/ping" ping
    get "/init" initDb
    getf "/sync/%s" sync
    put "/configurations" (putDocument Implementation.storeConfigurations)
    put "/transformations" (putDocument Implementation.storeTransformations)
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

run app