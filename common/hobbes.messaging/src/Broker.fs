namespace Hobbes.Messaging

open RabbitMQ.Client
open RabbitMQ.Client.Events
open System
open System.Text
open Hobbes.Helpers.Environment
open FSharp.Json

module Broker = 
    type CacheMessage = 
        Updated of key : string

    type SyncMessage = 
        Sync of string

    type TransformationMessageBody = 
        {
            Name : string
            Statements : seq<string>
        }
    type TransformMessage = 
        {
            Transformation : TransformationMessageBody
            CacheKey : string
        }
    type CalculationMessage = 
        Transform of TransformMessage

    let private user = 
        match env "RABBIT_USER" null with
        null -> failwith "'USER' not configured"
        | u -> u
    let private password =
        match env "RABBIT_PASSWORD" null with
        null -> failwith "'PASSWORD' not configured"
        | p -> p
    let private host = 
        match env "RABBIT_HOST" null with
        null -> failwith "Queue 'HOST' not configured"
        | h -> h
    let private port = 
        match env "RABBIT_PORT" null with
        null -> failwith "Post not specified"
        | p -> int p

    let private factory = ConnectionFactory()
    let private init() =
        try
            factory.HostName <- host
            factory.Port <- port
            factory.UserName <- user
            factory.Password <- password
            let connection = factory.CreateConnection()
            let channel = connection.CreateModel()
            channel
        with e ->
            eprintfn "Failed to initialize queue. %s:%d. Message: %s" host port e.Message
            reraise()

    let rec awaitQueue() = 
        async{
            try
                let channel = init()
                channel.QueueDeclare("logging",
                                     true,
                                     false,
                                     false,
                                     null) |> ignore
            with e -> 
               printfn "Queue not yet ready. Message: %s" e.Message
               do! Async.Sleep 1000
               do! awaitQueue()
        } 

    let private declare (channel : IModel) queueName =  
        channel.QueueDeclare(queueName,
                                 true,
                                 false,
                                 false,
                                 null) |> ignore

    let private watch<'a> queueName (handler : 'a -> bool) =
        let mutable keepAlive = true
        try
            let channel = init()
            declare channel queueName

            let consumer = EventingBasicConsumer(channel)
            consumer.Received.AddHandler(EventHandler<BasicDeliverEventArgs>(fun _ (ea : BasicDeliverEventArgs) ->
                try
                    let msg = 
                        Encoding.UTF8.GetString(ea.Body.ToArray())
                    if msg |> Json.deserialize<'a> |> handler then
                        printfn "Message ack'ed"
                        channel.BasicAck(ea.DeliveryTag,false)
                    else
                        printfn "Message could not be processed. %s" msg
                with e ->
                   eprintfn  "Failed while processing message. %s %s" e.Message e.StackTrace
            ))
            
            channel.BasicConsume(queueName,false,consumer) |> ignore
            printfn "Watching queue: %s" queueName
            while keepAlive do
                System.Threading.Thread.Sleep(60000)
         with e ->
           eprintfn "Failed to subscribe to the queue. %s:%d. Message: %s" host port e.Message
           keepAlive <- false

    let private publishString queueName (message : string) =
        try
            let channel = init()
            declare channel queueName
            
            let body = ReadOnlyMemory<byte>(Text.Encoding.UTF8.GetBytes(message))
            let properties = channel.CreateBasicProperties()
            properties.Persistent <- true

            channel.BasicPublish("",queueName, false,properties,body)
            printfn "Message published to %s" queueName
        with e -> 
           eprintfn "Failed to publish to the queue. Message: %s" e.Message
    let private publish<'a> queueName (message : 'a) =
        message
        |> Json.serialize
        |> publishString queueName
    type Broker() =
        static member Cache(msg : CacheMessage) = 
            publish "cache" msg
        static member Cache (handler : CacheMessage -> _) = 
            watch "cache" handler
        static member AzureDevOps(msg : SyncMessage) = 
            publish "azuredevops" msg
        static member AzureDevOps (handler : SyncMessage -> _) = 
            watch "azuredevops" handler
        static member Git(msg : SyncMessage) = 
            publish "git" msg
        static member Git (handler : SyncMessage -> _) = 
            watch "git" handler
        static member Calculation(msg : CalculationMessage) = 
            publish "calculation" msg
        static member Calculation (handler : CalculationMessage -> _) = 
            watch "calculation" handler
        static member Generic queueName msg =
            publishString queueName msg