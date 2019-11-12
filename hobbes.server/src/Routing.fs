module Routing
open FSharp.Control.Tasks.V2.ContextInsensitive
open Saturn
open Giraffe
open Microsoft.AspNetCore.Http
open Hobbes.Server.Db
open Hobbes.Server.Security
open System


let private watch = 
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
           
let rec private execute name f : HttpHandler =
    fun next (ctx : HttpContext) ->
            task {
                let start = watch.ElapsedMilliseconds
                let code, body = f ctx
                let ``end`` = watch.ElapsedMilliseconds
                Log.timed name (start - ``end``)
                return! (setStatusCode code >=> setBodyFromString body) next ctx
            }

let noArgs name f = execute name (fun ctx -> f())

let withArgs name f args =  
    execute name (fun context -> f context args)

let withBody name f args : HttpHandler = 
    fun next (ctx : HttpContext) ->
        task {
            let! body = ctx.ReadBodyFromRequestAsync()
            let f = f body
            return! ((withArgs name (f) args) next ctx)
        } 

let withBodyNoArgs name f : HttpHandler = 
    withBody name (fun body _ -> fun _ -> f body) ()

let skipContext f _ = f

let verifiedPipe = 
    pipeline {
        plug (fun next ctx -> 
                if verify ctx then
                    (setStatusCode 200) next ctx
                else
                    (setStatusCode 403 >=> setBodyFromString "unauthorized") next ctx
            )
    }