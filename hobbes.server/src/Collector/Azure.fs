module Hobbes.Server.Collector

open FSharp.Data
open System
open Hobbes.Server.Db

module Azure = 
    type private AzureWorkItems = JsonProvider<"""[{"@odata.nextLink":"https"},{"@odata.nextLink":"https"}]""", SampleIsList = true>
    type private Cookies = JsonProvider<"""[["foo","bar"]]"""> //JsonProvider<"cookies.json">

    let get projectName = 
        let rec read (stopwatch : Diagnostics.Stopwatch) (url : string) = 
            let id = System.Web.HttpUtility.UrlEncode url
            let start = stopwatch.ElapsedMilliseconds

            let dataJson = 
                printfn "Requesting %s" url
                Http.RequestString(url,
                    httpMethod = "GET",
                    cookies = 
                        (Cookies.GetSamples()
                         |> Array.map(function
                                       [|name;value|] -> name,value
                                       | a -> failwithf "No idead what to do with: %A" a
                         ))
                )
            printfn "Data received in %ims" (stopwatch.ElapsedMilliseconds - start)
            Rawdata.store (sprintf "azure:%s" projectName) id dataJson |> ignore
            let nextlink = (AzureWorkItems.Parse dataJson).OdataNextLink
            if String.IsNullOrWhiteSpace(nextlink) |> not then
                read stopwatch nextlink  
                
        let url = 
            projectName
            |> sprintf "https://analytics.dev.azure.com/kmddk/%s/_odata/v2.0/WorkItemRevisions?%%24expand=Iteration&%%24filter=IsLastRevisionOfDay%%20eq%%20true&orderby=Iteration.StartDate%%20desc" 
        
        read (Diagnostics.Stopwatch())  url