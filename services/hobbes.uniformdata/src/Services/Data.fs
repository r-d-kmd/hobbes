namespace Hobbes.UniformData.Services

open Hobbes.Web.Routing
open Hobbes.Web
open Hobbes.Helpers
open Hobbes.Messaging.Broker
open Hobbes.Messaging

[<RouteArea ("/data", false)>]
module Data =
    let private cache = Cache.Cache("uniform")
    [<Get ("/read/%s")>]
    let read key =
        let uniformData =
           key
           |> cache.Get 
            
        match uniformData with
        Some uniformData ->
            let data = uniformData.Data
            200, (uniformData |> Json.serialize)
        | None -> 
            404,"No data found"

    [<Post ("/update", true)>]
    let update dataAndKey =
        try
            let args = 
                dataAndKey 
                |> Json.deserialize<Cache.CacheRecord>

            
            let key = args.CacheKey
            let data = args.Data
            Log.logf "updating cache with _id: %s" key
            try    
                data
                |> cache.InsertOrUpdate key
                Broker.Cache (Updated key)
            with e ->
                Log.excf e "Failed to insert %s" key
            200, "updated"
        with e -> 
            Log.excf e "Couldn't update (%s)" dataAndKey
            500,"Internal server error"
    [<Get "/ping">]
    let ping () =
        200, "ping - UniformData"