namespace Hobbes.UniformData.Services

open Hobbes.Server.Routing
open Hobbes.Web
open Hobbes.Helpers

[<RouteArea ("/data", false)>]
module Data =
    let private cache = Cache.Cache("uniform")
    [<Post ("/read", true)>]
    let read confDoc =
        let uniformData =
           confDoc
           |> hash
           |> cache.Get 
            
        match uniformData with
        Some uniformData ->
            let data = uniformData.Data
            
            assert(data.ColumnNames.Length > 0)
            assert(data.RowCount = data.Rows.Length)
            assert(data.RowCount = 0 || data.ColumnNames.Length = data.Rows.[0].Numbers.Length + data.Rows.[0].Strings.Length)

            Log.logf "Data returned: %s" (uniformData.JsonValue.ToString())

            200, (uniformData.JsonValue.ToString())
        | None -> 
            404,"No data found"

    [<Post ("/update", true)>]
    let update conf =
        async {
            let sourceName = (Hobbes.Shared.RawdataTypes.Config.Parse conf).Source.Name
            if System.String.IsNullOrWhiteSpace sourceName then
                 failwithf "No source provided. %s" conf
                 
            Log.logf "Reading new data for %s" conf
            match Http.post (Http.Generic sourceName) Cache.CacheRecord.Parse "/read" conf with
            Http.Success cacheRecord ->
                Log.logf "updating cache for %s with _id: %s" sourceName cacheRecord.Id
                try
                    cacheRecord
                    |> cache.InsertOrUpdate
                with e ->
                    Log.excf e "Failed to insert %s" cacheRecord.Id
            | Http.Error(status,m) ->
                Log.errorf null "Failed to read data from %s. Status: %d - Message: %s" conf status m
        } |> Async.Start
        200, "updating"

    [<Get "/ping">]
    let ping () =
        200, "ping - UniformData"