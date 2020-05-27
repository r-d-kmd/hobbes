namespace Hobbes.UniformData.Services

open Hobbes.Web.Routing
open Hobbes.Web
open Hobbes.Web.RawdataTypes
open Hobbes.Messaging.Queue
open FSharp.Data

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

            Log.debugf "Data returned: %s" (uniformData.JsonValue.ToString())

            200, (uniformData.JsonValue.ToString())
        | None -> 
            404,"No data found"

    [<Post ("/update", true)>]
    let update dataAndKey =
        async {
            let args = 
                dataAndKey 
                |> Cache.UpdateArguments.Parse
            let key = args.String
            let data = args.Record
            Log.logf "updating cache with _id: %s" key
            try
                let dataRecord = 
                    data.JsonValue.ToString() 
                    |> Cache.DataResult.Parse
                dataRecord
                |> cache.InsertOrUpdate key
                publish Queue.Cache key
            with e ->
                Log.excf e "Failed to insert %s" key
        } |> Async.Start
        200, "updating"

    [<Get "/ping">]
    let ping () =
        200, "ping - UniformData"