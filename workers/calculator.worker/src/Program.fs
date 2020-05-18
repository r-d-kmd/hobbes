open System
open System.Text
open Hobbes.Helpers.Environment
open Hobbes.Web
open Hobbes.Web.RawdataTypes


type CalculationMessage = FSharp.Data.JsonProvider<"""{
       "calculationSource" : "cacheKey",
       "transformationName" : "transformation" 
    }""">

let cache = Cache.Cache(Http.UniformData)
let handleMessage message = 
    let calculationMessage = CalculationMessage.Parse message
    let data = cache.Get calculationMessage.CalculationSource
    match Http.get (calculationMessage.TransformationName |> Some |> Http.Transformation |> Http.Configurations) id  with
    Http.Success transformation ->
        failwith "Not implemented yet"
    | Http.Error(sc,m) ->
        eprintfn "Calculation failed. Couldn't get transformation. %d - %s" sc m
        false
[<EntryPoint>]
let main _ =
    Hobbes.Workers.Shared.Queue.watch handleMessage
    printfn "Waiting for calculation messages"
    printfn "Press enter to exit"
    let a = Console.ReadLine() 
    printfn "%s" a
    0