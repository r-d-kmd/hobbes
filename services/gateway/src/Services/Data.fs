namespace Hobbes.Gateway.Services

open Hobbes.Web.Log
open Hobbes.Web.Routing
open Hobbes.Helpers
open Hobbes.Web

[<RouteArea "/data">]
module Data = 
    let private cacheRevision confDoc = 
        sprintf "%s:%d" confDoc (System.DateTime.Now.Ticks) |> hash

    let rowToString =
        function
          Cache.Value.Int i -> sprintf "%i" i
        | Cache.Value.Float f -> sprintf "%f" f
        | Cache.Value.Date d -> sprintf "\"%A\"" d
        | Cache.Value.Text s -> s |> Json.serialize
                                |> sprintf "%A"
        | Cache.Value.Null -> "null"
        | Cache.Value.Boolean b -> sprintf "%b" b

    [<Get ("/json/%s")>]
    let json configuration =  
        debugf "Getting json for '%A'" configuration
        match Http.get (configuration |> Some |> Http.Configuration |> Http.Configurations) RawdataTypes.Config.Parse with
        Http.Success config -> 
            let key = config |> RawdataTypes.keyFromConfig
            match Http.get (key |> Http.CacheService.Read |> Http.UniformData) id with
            Http.Success json ->
                let record = Json.deserialize<Cache.CacheRecord> json
                let names = record.Data.ColumnNames
                let rows = record.Data.Values
                let threads = 8
                let rowCount = record.Data.RowCount
                let stepSize = 
                    if rowCount % threads = 0 then
                       threads 
                    else 
                       threads - 1
                let join delim (s : seq<string>) = System.String.Join(delim,s)
                let formatted = 
                    seq { for i in stepSize .. rowCount .. stepSize ->
                            rows 
                            |> Seq.skip(i - stepSize)
                            |> Seq.take(min (stepSize + rowCount - i) stepSize) 
                    } |> Seq.map (fun rowSection -> 
                        async{
                            return 
                                rowSection
                                |> Seq.map(
                                    Array.map2 (fun n v -> 
                                        sprintf "\"%s\" : %s" (Json.serialize n) (rowToString v)
                                    ) names
                                    >> join ","
                                    >> sprintf "{%s}"
                                ) |> join ","
                        }
                    )
                    |> Async.Parallel
                    |> Async.RunSynchronously
                    |> join ","
                    |> sprintf "[%s]"

                200, formatted
            | Http.Error(sc,m) -> sc,sprintf "Data for configuration %s not found. Message: %s" configuration m
        | Http.Error(sc,m) -> sc,sprintf "Configuration %s not found. Message: %s" configuration m
    