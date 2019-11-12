#load "../bin/Debug/netcoreapp3.0/hobbes.server.dll"
(***hide***)
let ``/status/sync`` = Implementation.getSyncState
(**
## GET /status/sync

The syncronization jobs are (potentially) long running server processes. The request to syncronize returns imediately providing a syncId.
Passing this syncId to the /status/sync end point will return a JSON object related to that syncronization job

    {
      "_id": "azure devops:flowerpot",
      "_rev": "22-fdcf1158175ff7c8ced12cb386a2fabe",
      "source": "Azure DevOps",
      "project": "flowerpot",
      "timeStamp": "11/06/2019 10:19:03",
      "data": null,
      "revision": "7b88851ac5b1a0ba3c5d1190f82f6ae5",
      "state": "synced"
    }
*)
``/status/sync`` "syncId"

(**
get "/ping" ((ignore >> Implementation.ping) |> Routing.unverified "ping" ) 
getf "/key/%s" (Implementation.key |> Routing.skipContext |> Routing.unverifiedWithArgs "key")

getf "/csv/%s" (Implementation.csv |> Routing.skipContext |> Routing.verifiedWithArgs "csv" ) 
getf "/sync/%s" ( sync |> Routing.verifiedWithArgs "sync" )
getf "/raw/%s" (Rawdata.get |> Routing.skipContext |> Routing.verifiedWithArgs "raw" )

put "/configurations" (Implementation.storeConfigurations |> Routing.withBodyNoArgs "configurations")
put "/transformations" (Implementation.storeTransformations |> Routing.withBodyNoArgs "transformations")

get "/list/configurations" (Implementation.listConfigurations  |> Routing.verified "list/configurations")
get "/list/transformations" (Implementation.listTransformations |> Routing.verified "list/transformations" )
get "/list/cache" (Implementation.listCache |> Routing.verified "list/cache")
get "/list/rawdata" (Implementation.listRawdata |> Routing.verified "list/rawdata")
get "/list/log" (Implementation.listLog |> Routing.verified "list/log")

deletef "/raw/%s" (Rawdata.delete |> Routing.skipContext |> Routing.verifiedWithArgs "raw" )
deletef "/cache/%s" (Cache.delete |> Routing.skipContext |> Routing.verifiedWithArgs "cache" )
getf "/status/sync/%s" (Implementation.getSyncState |> Routing.skipContext |> Routing.verifiedWithArgs  "status/sync")
getf "/admin/settings/%s/%s" ( Implementation.setting |> Routing.skipContext |> Routing.verifiedWithArgs "admin/settings")
putf "/admin/settings/%s/%s/%s" (Implementation.configure |> Routing.skipContext  |> Routing.verifiedWithArgs "admin/settings") *)