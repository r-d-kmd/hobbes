module Routing
open FSharp.Control.Tasks.V2.ContextInsensitive
open Saturn
open Giraffe
open Microsoft.AspNetCore.Http
open Hobbes.Server.Db.Database
open Hobbes.Server.Db
open Hobbes.Server.Security
open System


let private watch = 
    let w = Diagnostics.Stopwatch()
    w.Start()
    w

type Request = 
    Verified of name : string
    | Unverified of name: string

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

let skipContext f _ = f

let verifiedPipe = 
    pipeline {
        plug (fun next ctx -> 
                task { 
                    return
                        if verify ctx then
                            ctx |> Some
                        else
                            failwith "Unauthorized"
                })
    }