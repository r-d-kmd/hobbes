module Routing
open Saturn
open Giraffe
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks.V2.ContextInsensitive
open System
open Hobbes.AzureDevopsCollector.Db

let private watch = 
    let w = Diagnostics.Stopwatch()
    w.Start()
    w

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

let skipContext f _ = f