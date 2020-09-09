
open Hobbes.Web
open Hobbes.Messaging
open Hobbes.Messaging.Broker
open Hobbes.Helpers

type CollectorList = FSharp.Data.JsonProvider<"""["azure devops","git"]""">
type SourceList = FSharp.Data.JsonProvider<"""[{
                "provider" : "azuredevops",
                "id" : "lkjlkj", 
                "project" : "gandalf",
                "dataset" : "commits",
                "server" : "https://analytics.dev.azure.com/kmddk/flowerpot"
            },{
                "provider" : "azuredevops",
                "id" : "lkjlkj", 
                "project" : "gandalf",
                "dataset" : "commits",
                "server" : "https://analytics.dev.azure.com/kmddk/flowerpot"
            }]""">
[<EntryPoint>]
let main _ =
    
    async{
        do! awaitQueue()
    
        match Http.get (Http.Configurations Http.Collectors) CollectorList.Parse  with
        Http.Success collectors ->
            let sources =  
                collectors
                |> Array.collect(fun collector ->
                    match Http.get (Http.Configurations (Http.Sources collector)) SourceList.Parse  with
                    Http.Success sources ->
                        sources
                    | Http.Error (sc,m) -> 
                        failwithf "Failed retrievining sources. %d - %s" sc m
                ) 
            Log.debugf "Syncronizing %d sources" (sources |> Seq.length)
            let semp = obj()
            let mutable set = Map.empty
            let handleMessage (msg : SyncronizationTicketMessage) = 
                lock semp (fun () ->
                    set <- set.Remove msg.SourceHash
                )
                Success
            let sync (source: SourceList.Root) = 
                let queueName = source.Provider.ToLower().Replace(" ","")
                let json = source.JsonValue.ToString()
                json
                |> Sync
                |> Message
                |> Newtonsoft.Json.JsonConvert.SerializeObject
                |> Broker.Generic queueName
            async {    
                Broker.SyncronizationTicket handleMessage
            } |> Async.Start
            set <- 
                sources
                |> Array.fold(fun set' source ->
                    (source.JsonValue.ToString() |> RawdataTypes.keyFromSourceDoc,source) |> set'.Add
                ) set
            sources |> Array.iter sync
            
            let timeout = env "SYNC_TIMEOUT" "300" |> int
            let rec waitForSync counter = 
                System.Threading.Thread.Sleep(1000)
                if (set.Count > 0) && (counter < timeout) then
                    if counter % 60 = 1 then 
                        printf "Waiting for %A" (set |> Map.toList |> List.map (fun (_,source) -> source.Provider + "-" + source.Project))
                    waitForSync (counter + 1)
            waitForSync 0
            set
            |> Map.iter (fun _ source -> sync source)
            waitForSync 0
            
        | Http.Error(sc,m) -> 
            failwithf "Failed retrievining collectors. %d - %s" sc m
            
    } |> Async.RunSynchronously
    0