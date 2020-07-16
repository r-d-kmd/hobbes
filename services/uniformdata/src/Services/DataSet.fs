namespace Hobbes.UniformData.Services

open Hobbes.Web.Routing
open Hobbes.Web
open Hobbes.Messaging.Broker
open Hobbes.Messaging
open FSharp.Data

[<RouteArea ("/dataset", false)>]
module DataSet =
    let private cache = Cache.dynamicCache("uniform")

    let private dataToString data =
        Array.map (fun (d : Cache.DynamicRecord.Datum) -> d.JsonValue.ToString()) data
        |> String.concat ","
        |> sprintf "[%s]"

    [<Get ("/read/%s")>]
    let read key =
        let dataSet =
           key
           |> cache.Get 
            
        match dataSet with
        Some dataSet ->
            200, dataToString dataSet.Data
        | None -> 
            404,"No data found"

    [<Put ("/update", true)>]
    let update dataAndKey =
        try
            let args =
                try
                    dataAndKey
                    |> Cache.DynamicRecord.Parse
                    |> Some
                with e ->
                    eprintfn "Failed to deserialization (%s). %s %s" dataAndKey e.Message e.StackTrace
                    None
            match args with
            Some args ->
                let key = args.Id
                let data = args.Data
                let dependsOn = args.DependsOn |> Array.toList
                Log.logf "updating cache with _id: %s" key
                try
                    data
                    |> Array.map(fun d -> d.JsonValue)
                    |> cache.InsertOrUpdate key dependsOn
                with e ->
                    Log.excf e "Failed to insert %s" (dataAndKey.Substring(0,min 500 dataAndKey.Length))
                200, "updated"
            | None -> 400,"Failed to parse payload"
        with e -> 
            Log.excf e "Couldn't update"
            500,"Internal server error"
    [<Get "/ping">]
    let ping () =
        200, "ping - DataSet"