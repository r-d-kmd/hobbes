open FSharp.Control.Tasks.V2.ContextInsensitive
open Saturn
open Saturn.Controller
open Giraffe
open Giraffe.Core
open Giraffe.ResponseWriters
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
                    f ctx
                else 
                    403, "Unauthorized"
        (setStatusCode statusCode
         >=> setBodyFromString body) func ctx

let private data configurationName =
    verified (fun _ -> Implementation.data configurationName)

let private sync configurationName =
    let executer (ctx : HttpContext) =
        try
            match ctx.TryGetRequestHeader "PAT" with
            Some pat ->
                Implementation.sync pat configurationName
            | None -> 403,"Unauthorized"
        with e -> 
            eprintfn "Couldn't sync %s. Reason: %s" configurationName e.Message
            500, e.Message
            
    verified executer

let private putDocument (db : Database<'a>) name =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let! body = ctx.ReadBodyFromRequestAsync()
            return! verified (fun _ -> Implementation.putDocument db name body) next ctx
        }

let private initDb =
    let (sc, body) = Implementation.initDb
    setStatusCode sc >=> setBodyFromString body

let private key token =
    let statusCode,body = Implementation.key token
    setStatusCode statusCode >=> setBodyFromString body

let private apiRouter = router {
    not_found_handler (setStatusCode 404 >=> text "Api 404")
    
    getf "/data/%s" data
    getf "/key/%s" key
    get "/ping" (setStatusCode 200 >=> setBodyFromString "pong")
    get "/init" initDb
    getf "/sync/%s" sync
    putf "/configurations/%s" (putDocument configurations)
    putf "/transformations/%s" (putDocument transformations)
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