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

    [<Get "/ping">]
    let ping () =
        200, "ping - UniformData"