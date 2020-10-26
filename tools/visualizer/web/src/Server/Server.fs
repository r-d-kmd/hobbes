module Server

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Saturn
open Shared

let cacheDir =
    let name = ".cache"
    if System.IO.Directory.Exists name |> not then
        System.IO.Directory.CreateDirectory name |> ignore
    name

let chartsApi =
    { getChart =
        fun id ->
            async {
                let! data =
                    try
                        Reader.read id
                    with e ->
                        printfn "Failed to read data. %s %s" e.Message e.StackTrace
                        reraise()
                return data
            }
      getAreas =
          fun () ->
             async{
                 return
                     System.IO.Directory.EnumerateDirectories "transformations"
                     |> Seq.map(fun dir ->
                         let area = System.IO.Path.GetFileNameWithoutExtension dir
                         printfn "Looking in %s" area
                         let chartIds =
                             System.IO.Directory.EnumerateFiles(dir,"*.hb")
                             |> Seq.map(System.IO.Path.GetFileNameWithoutExtension)
                             |> List.ofSeq
                         let area =
                             {
                                 Id = area
                                 ChartIds = chartIds
                             }
                         printfn "Area %A" area
                         area
                     ) |> List.ofSeq
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
Reader.cache()
run app
