module Collector.Implementation

let ping () =
    200, "pong"

let getSyncState syncId =
    500, "not implemented yet"

let sync azureToken configurationName =
    500, "not implemented yet"

let initDb() = 200,"ok"