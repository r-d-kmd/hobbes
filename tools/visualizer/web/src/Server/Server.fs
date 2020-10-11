module Server

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Saturn
open Shared

let primes = [|2.;3.;5.;7.;9.;11.;13.;17.|]
let testData name =
    [ for j in 1..2 ->
        {
            Id = name + string(j)
            ChartType = Column
            Data =
              [|
                  [| for i in 0..100 ->
                      {
                          Y =
                             if j = 1 then float(i) / primes.[i % primes.Length] |> float |> YValue.Number
                             else
                                 float(i) |> YValue.Number
                          X = i |> float
                      }
                  |]
              |]
        }
    ]
let chartsApi =
    { getCharts =
        fun name ->
            let data = testData name
            printfn "Gettting %s %d" name (data.Length)
            async {
                return data
            }
    }

let webApp =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue chartsApi
    |> Remoting.buildHttpHandler

let app =
    application {
        url "http://0.0.0.0:8085"
        use_router webApp
        memory_cache
        use_static "public"
        use_gzip
    }

run app
