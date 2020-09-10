
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
                        printfn "Waiting for action to quite down"     
                        waitForSync (-1 * (delta + 1.0 |> int))
                else
                    printfn "Completed syncronization"
                    printfn "Exceptions:"
                    exceptions
                    |> Seq.iter(fun (m,json) -> eprintfn "Message: %s. Message: %s" m json)
                    printfn "Failures:"
                    failures
                    |> Seq.iter(fun (m,json) -> eprintfn "Message: %s. Message: %s" m json)
            waitForSync 1
        | Http.Error(sc,m) -> 
            failwithf "Failed retrievining collectors. %d - %s" sc m
            
    } |> Async.RunSynchronously
    0