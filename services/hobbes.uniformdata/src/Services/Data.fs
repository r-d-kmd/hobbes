namespace Hobbes.UniformData.Services

open Hobbes.Web.Routing
open Hobbes.Web
open Hobbes.Web.RawdataTypes
open Hobbes.Workers.Shared.Queue

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
            
            assert(data.ColumnNames.Length > 0)
            assert(data.RowCount = data.Rows.Length)
            assert(data.RowCount = 0 || data.ColumnNames.Length = data.Rows.[0].Numbers.Length + data.Rows.[0].Strings.Length)

            Log.logf "Data returned: %s" (uniformData.JsonValue.ToString())

            200, (uniformData.JsonValue.ToString())
        | None -> 
            404,"No data found"

    [<Post ("/update", true)>]
    let update cacheRecordDoc =
        async {
            let cacheRecord = cacheRecordDoc |> Hobbes.Web.Cache.CacheRecord.Parse
            Log.logf "updating cache with _id: %s" cacheRecord.Id
            try
                cacheRecord
                |> cache.InsertOrUpdate
                publish Queue.Cache cacheRecordDoc
            with e ->
                Log.excf e "Failed to insert %s" cacheRecord.Id
        } |> Async.Start
        200, "updating"

    [<Get "/ping">]
    let ping () =
        200, "ping - UniformData"