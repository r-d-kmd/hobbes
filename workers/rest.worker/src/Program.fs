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

let private transform (transformation : string)  data = 
    let compiledChunks = transformation |> Hobbes.FSharp.Compile.compile
    compiledChunks
    |> Seq.collect(fun c -> c.Blocks)
    |> Seq.fold(fun data t ->
        match t with
        Transformation t -> t data
        | _ -> data
    ) data

let rec work() = 
    let success,value = 
        if workQueue |> isNull then
            failwith "How on earth did that happen?"
        else
            workQueue.TryDequeue()
    if success then
        printfn "Found work to do"
        let restProvider, transformation = value
        try
            printfn "Requesting data: %A" (restProvider.Url)
            let data : Hobbes.FSharp.DataStructures.IDataMatrix = 
                restProvider
                |> read 
                |> Hobbes.FSharp.DataStructures.DataMatrix.fromTable
            assert(data.RowCount > 0)
            printfn "Read data from provider"
            let dataAsJson =         
                data
                |> transform transformation
                |> Hobbes.FSharp.DataStructures.DataMatrix.toJson
            
            printfn "Data transformed"
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
                               
            let result =                   
                Encode.object [
                    "_id", Encode.string restProvider.Name
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
               printfn "Data uploaded to cache"
            | Http.Error(status,msg) -> 
                Log.errorf "Upload of %s to uniform data failed. %d %s" result status msg
        with e ->
            Log.excf e "Error while transforming data for %s" restProvider.Name
            System.Threading.Thread.Sleep 1000
            work()
    else
        printfn "Waiting for something to do"
    

let enqueue restProvider transformation = 
    if workQueue.IsEmpty then
        workQueue.Enqueue(restProvider,transformation)
        work()
    else
        workQueue.Enqueue(restProvider, transformation)

let private handleMessage message =
    match message with
    Empty -> 
        printfn "Received empty message" 
        Success
    | Sync(name,configDoc) -> 
        printfn "Received message. %s" configDoc
        try
            let configuration = 
                configDoc |> Config.Parse
            let meta = 
                match configuration.Source.Meta.JsonValue with
                FSharp.Data.JsonValue.Record properties -> properties
                | v -> failwithf "Expected an object got %A" v
            let source = configuration.Source
            let urls = source.Urls 
            assert(urls.Length > 0)
            let restProvider = 
                {
                    Url = source.Urls
                    Values = source.Values
                    User   = source.User
                    Pwd    = source.Pwd
                    Meta   = meta
                    Name   = name
                }

            enqueue restProvider configuration.Transformation
            Success
        with e ->
            printfn "Failed to process message. %s %s" e.Message e.StackTrace
            Excep e

[<EntryPoint>]
let main _ =
    Database.awaitDbServer()
    async{    
        do! awaitQueue()
        Broker.Rest handleMessage
    } |> Async.RunSynchronously
    0