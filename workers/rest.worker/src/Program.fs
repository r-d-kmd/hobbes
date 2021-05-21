open Hobbes.Helpers.Environment
open Hobbes.Web
open Hobbes.Messaging
open Hobbes.Messaging.Broker
open Hobbes.FSharp.Compile
open Hobbes.Web.RawdataTypes
open Hobbes.Workers.RestProvider
open Thoth.Json.Net
open System.Collections.Concurrent

//we're using a queue to make sure the worker is single tasking.
//working on two jobs might exhaust ressources!
let private workQueue = ConcurrentQueue<Source * _>()

let private transform (transformation : Hobbes.FSharp.DataStructures.IDataMatrix -> Hobbes.FSharp.DataStructures.IDataMatrix)  data = 
    data
    |> transformation

let rec work() = 
    let success,(restProvider, transformation) = 
        workQueue.TryDequeue()
    if success then
        try
            let data : Hobbes.FSharp.DataStructures.IDataMatrix = 
                restProvider
                |> read 
                |> Hobbes.FSharp.DataStructures.DataMatrix.fromTable
            let dataAsJson = 
                match transformation with
                  None -> data
                  | Some t -> 
                      data
                      |> transform t
                |> Hobbes.FSharp.DataStructures.DataMatrix.toJson
            let result = 
                let rec encode = 
                    function
                        FSharp.Data.JsonValue.String s -> Encode.string s
                        | FSharp.Data.JsonValue.Number d -> Encode.decimal d
                        | FSharp.Data.JsonValue.Float f -> Encode.float f
                        | FSharp.Data.JsonValue.Boolean b ->  Encode.bool b
                        | FSharp.Data.JsonValue.Record properties ->
                            properties
                            |> Array.map(fun (name,v) ->
                                name, v |> encode
                            ) |> List.ofArray
                            |> Encode.object
                        | FSharp.Data.JsonValue.Array elements ->
                             elements
                             |> Array.map encode
                             |> Encode.array
                        | FSharp.Data.JsonValue.Null -> Encode.nil
                                   
                              
                Encode.object [
                    "data",dataAsJson
                    "name", Encode.string restProvider.Name
                    "meta", 
                       restProvider.Meta
                       |> Array.map(fun (name,value) ->
                           name, value |> encode
                       ) |> List.ofArray
                       |> Encode.object
                ] |> Encode.toString 0
            match Http.post (Http.UniformData Http.Update) result with
            Http.Success _ -> 
               Log.logf "Data uploaded to cache"
            | Http.Error(status,msg) -> 
                Log.errorf "Upload of %s to uniform data failed. %d %s" result status msg
        with e ->
            Log.excf e "Error while transforming data for %s" restProvider.Name
        work()

let enqueue restProvider transformation = 
    if workQueue.IsEmpty then
        workQueue.Enqueue(restProvider,transformation)
        work()
    else
        workQueue.Enqueue(restProvider, transformation)

let private handleMessage message =
    match message with
    Empty -> Success
    | Sync(name,configDoc) -> 
        
        Log.debugf "Received message. %s" configDoc
        try
            let configuration = 
                configDoc |> Config.Parse
            let meta = 
                match configuration.Source.Meta.JsonValue with
                FSharp.Data.JsonValue.Record properties -> properties
                | v -> failwithf "Expected an object got %A" v
            let source = configuration.Source
            let restProvider = 
                {
                    Url = source.Urls
                    Values = source.Values
                    User   = source.User
                    Pwd    = source.Pwd
                    Meta   = meta
                    Name   = name
                }

            let transformation = 
                match (configuration.Transformation |> Hobbes.FSharp.Compile.compile).Head.Blocks
                    |> Seq.filter(function
                                    Transformation _ -> true
                                    | _ -> false
                    ) |> List.ofSeq with
                [Transformation t] -> Some t
                | [] -> None
                | _ -> failwith "should have been removed"
            enqueue restProvider transformation
            work()
            Success
        with e ->
            Log.excf e "Failed to process message"
            Excep e

[<EntryPoint>]
let main _ =
    Database.awaitDbServer()
    async{    
        do! awaitQueue()
        Broker.Rest handleMessage
    } |> Async.RunSynchronously
    0