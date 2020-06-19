namespace Hobbes.UniformData.Services

open Hobbes.Web.Routing
open Hobbes.Web
open Hobbes.Helpers
open Hobbes.Messaging.Broker
open Hobbes.Messaging

[<RouteArea ("/dataset", false)>]
module DataSet =
    let private cache = Cache.GenericCache("uniform")
    [<Get ("/read/%s")>]
    let read key =
        let uniformData =
           key
           |> cache.Get 
            
        match uniformData with
        Some uniformData ->
            200, (uniformData |> Json.serialize)
        | None -> 
            404,"No data found"

    [<Post ("/update", true)>]
    let update dataAndKey =
        try
            let args =
                try
                    dataAndKey
                    |> Cache.DynamicCacheRecord.Parse
                    |> Some
                with e ->
                    eprintfn "Failed to deserialization (%s). %s %s" dataAndKey e.Message e.StackTrace
                    None
            match args with
            Some args ->
                let key = args.Id
                let data = args.Data
                Log.logf "updating cache with _id: %s" key
                try    
                    data.ToString()
                    |> cache.InsertOrUpdate key
                    Broker.Cache (Updated key)
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