open FSharp.Control.Tasks.V2.ContextInsensitive
open Saturn
open Giraffe
open Microsoft.AspNetCore.Http
open Hobbes.Server.Db.Database
open Hobbes.Server.Db
open Hobbes.Server.Security
open System

let private port = 
    match env "port" with
    null -> 8085
    | p -> int p

let watch = 
    let w = Diagnostics.Stopwatch()
    w.Start()
    w

let private verify (ctx : HttpContext) =
        let authToken = 
            let url = ctx.GetRequestUrl()
            printfn "Requesting access to %s" url
            match Uri(url) with
            uri when String.IsNullOrWhiteSpace(uri.UserInfo) |> not ->
                uri.UserInfo |> Some
            | _ -> 
                ctx.TryGetRequestHeader "Authorization"
                
        authToken
        |> Option.bind(fun authToken ->
            if authToken |> verifyAuthToken then
                Some authToken
            else 
                None
        ) |> Option.isSome
           
let private sync (ctx : HttpContext) configurationName =
        try
            match ctx.TryGetRequestHeader "PAT" with
            Some azurePAT -> Implementation.sync azurePAT configurationName
            | None ->  403, "Unauthorized"
        with e -> 
            Log.errorf e.StackTrace "Couldn't sync %s. Reason: %s" configurationName e.Message
            500, e.Message

type Request = 
    Verified of name : string
    | Unverified of name: string

let rec private execute (request : Request) f : HttpHandler =
    match request with
    | Unverified name ->
        fun next (ctx : HttpContext) ->
            task {
                let start = watch.ElapsedMilliseconds
                let code, body = f ctx
                let ``end`` = watch.ElapsedMilliseconds
                Log.timed name (start - ``end``)
                return! (setStatusCode code >=> setBodyFromString body) next ctx
            }
    | Verified name ->
        let f ctx = 
            if verify ctx then
                f ctx
            else
                403, "Unauthorized"
        execute (Unverified name) f

                    
let withArgs (request : Request) f args =  
    execute request (fun context -> f context args)

let verified name f = execute (name |> Verified) (fun ctx -> f())
let unverified name = execute (name |> Unverified)    
let verifiedWithArgs name = (name |> Verified |> withArgs) 
let unverifiedWithArgs name = (name |> Unverified |> withArgs)
let withBody name f args : HttpHandler = 
    fun next (ctx : HttpContext) ->
        task {
            let! body = ctx.ReadBodyFromRequestAsync()
            let f = f body
            return! ((withArgs (Verified name) (f) args) next ctx)
        } 

let withBodyNoArgs name f : HttpHandler = 
    withBody name (fun body _ -> fun _ -> f body) ()

let skipContext f ctx = f

let private apiRouter = router {
    not_found_handler (setStatusCode 404 >=> text "Api 404")
    
    get "/ping" (("ping" |> unverified) (ignore >> Implementation.ping))
    get "/init" (("initDb" |> verified) (ignore >> Implementation.initDb))
    
    getf "/key/%s" (Implementation.key |> skipContext |> unverifiedWithArgs "key")
    getf "/csv/%s" (skipContext Implementation.csv |> verifiedWithArgs "csv" ) 
    getf "/sync/%s" ( sync |> verifiedWithArgs "sync" )

    put "/configurations" (Implementation.storeConfigurations |> withBodyNoArgs "configurations")
    put "/transformations" (Implementation.storeTransformations |> withBodyNoArgs "transformations")
    get "/list/configurations" (Implementation.listConfigurations  |> verified "list/configurations")
    get "/list/transformations" (Implementation.listTransformations |> verified "list/transformations" )
    get "/list/cache" (Implementation.listCache |> verified "list/cache")
    get "/list/rawdata" (Implementation.listRawdata |> verified "list/rawdata")
    getf "/status/sync/%s" (skipContext Implementation.getSyncState |> verifiedWithArgs  "status/sync")
    getf "/admin/settings/%s/%s" ( skipContext Implementation.setting |> verifiedWithArgs "admin/settings")
    putf "/admin/settings/%s/%s/%s" (skipContext Implementation.configure |> verifiedWithArgs "admin/settings")
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