
open Hobbes.Web
open Hobbes.Web.RawdataTypes
open Hobbes.Messaging
open Hobbes.Messaging.Broker
open Hobbes.Helpers
open FSharp.Data

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

type DataSet = FSharp.Data.JsonProvider<"""
        {
          "_id": "34d147fc6e2509c0080e1dbd7f2337fa",
          "_rev": "1-aa275c7c28e46827114998269841843d",
          "timeStamp": "12/03/2020 15:22:42",
          "data": {
            "columnNames": [],
            "values": []
          }
        }
""">
[<EntryPoint>]
let main _ =
    
    async{
        do! awaitQueue()
        let timeOfSync = System.DateTime.Now
        let mutable timeOfLastMessage = System.DateTime.Now
        let failures = System.Collections.Generic.List()
        let exceptions = System.Collections.Generic.List()
        async {
            Broker.Log(function
                        MessageCompletion _ -> 
                            timeOfLastMessage <- System.DateTime.Now
                            Broker.Success
                        | MessageFailure(m,json) ->
                            Log.errorf "message processing failed %s - %s" m json
                            (m,json)
                            |> failures.Add
                            timeOfLastMessage <- System.DateTime.Now
                            Broker.Success
                        | MessageException(m,json) ->
                            Log.errorf "message processing failed with exception %s - %s" m json
                            (m,json)
                            |> exceptions.Add
                            timeOfLastMessage <- System.DateTime.Now
                            Broker.Success
            )
        } |> Async.Start
        match Http.get (Http.Configurations Http.Collectors) CollectorList.Parse  with
        Http.Success collectors ->
            let sources =  
                collectors
                |> Array.collect(fun collector ->
                    match Http.get (Http.Configurations (Http.Sources collector)) SourceList.Parse  with
                    Http.Success sources ->
                        printfn "%A" sources.[0]
                        sources
                    | Http.Error (sc,m) -> 
                        failwithf "Failed retrievining sources. %d - %s" sc m
                ) 
            Log.debugf "Syncronizing %d sources" (sources |> Seq.length)
            
            let sync (source: SourceList.Root) = 
                let queueName = source.Provider.ToLower().Replace(" ","")
                let json = source.JsonValue.ToString()
                json
                |> Sync
                |> Message
                |> Newtonsoft.Json.JsonConvert.SerializeObject
                |> Broker.Generic queueName
            sources |> Array.iter sync
            
            let timeout = System.DateTime.Now.AddSeconds(env "SYNC_TIMEOUT" "600" |> float)
            let idleTimeExpected = 45.0
            let rec waitForSync (waitTime : int) = 
                System.Threading.Thread.Sleep(waitTime * 1000)
                
                let delta = (System.DateTime.Now - timeOfLastMessage.AddSeconds(idleTimeExpected)).TotalSeconds
                if delta < 0.0 then
                    if System.DateTime.Now > timeout then
                        eprintfn "Timed out while syncronizing"
                    else 
                        waitForSync (-1 * (delta + 1.0 |> int))
                else
                    let mutable succesful = 0
                    let mutable failures  = 0
                    let mutable badStamp  = 0
                    printfn "succes: %A, failures: %A, bad timestamp: %A" succesful failures badStamp
                    printfn("Sync-moniter starting")
                    let x = Http.get (Http.Service.Configurations (Http.ConfigurationService.AllConfigurations)) Config.Parse

                    printfn "%A" x
                    
                    printfn "succes: %A, failures: %A, bad timestamp: %A" succesful failures badStamp
            waitForSync 1
            
        | Http.Error(sc,m) -> 
            failwithf "Failed retrievining collectors. %d - %s" sc m
            
    } |> Async.RunSynchronously
    0