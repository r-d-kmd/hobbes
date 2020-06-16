namespace Hobbes.Messaging

open RabbitMQ.Client
open RabbitMQ.Client.Events
open System
open System.Text
open Hobbes.Helpers.Environment
open Hobbes.Helpers

module Broker = 
    type CacheMessage = 
        Updated of string
        | Empty

    type SyncMessage = 
        Sync of string
        | Empty

    type DeadLetter =
        {
            OriginalQueue : string
            OriginalMessage : string
            ExceptionMessage : string
            ExceptionStackTrace : string
        }

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

    let private init() =
        let factory = ConnectionFactory()
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
    
    let private declare (channel : IModel) queueName =  
        channel.QueueDeclare(queueName,
                                 true,
                                 false,
                                 false,
                                 null) |> ignore
    let rec awaitQueue() = 
        async{
            try
                let channel = init()
                declare channel "dead_letter"
            with e -> 
               printfn "Queue not yet ready. Message: %s" e.Message
               do! Async.Sleep 5000
               do! awaitQueue()
        } 

    type MessageResult = 
        Success
        | Failure of string
        | Excep of System.Exception

    let private publishString queueName (message : string) =
        try
            let channel = init()
            declare channel queueName
            
            let body = ReadOnlyMemory<byte>(Text.Encoding.UTF8.GetBytes(message))
            let properties = channel.CreateBasicProperties()
            properties.Persistent <- true

            channel.BasicPublish("",queueName, false,properties,body)
            printfn "Message published to %s:%d/%s" host port queueName
        with e -> 
           eprintfn "Failed to publish to the queue. Message: %s" e.Message

    let private publish<'a> queueName (message : 'a) =    
        message
        |> Json.serialize
        |> publishString queueName

    let private watch<'a> queueName (handler : 'a -> MessageResult) =
        let mutable keepAlive = true
         
        try
            let channel = init()
            declare channel queueName

            let consumer = EventingBasicConsumer(channel)
            consumer.Received.AddHandler(EventHandler<BasicDeliverEventArgs>(fun _ (ea : BasicDeliverEventArgs) ->
                let deadLetter msgText (e : System.Exception) =
                    {
                        OriginalQueue = queueName
                        OriginalMessage = msgText
                        ExceptionMessage = e.Message
                        ExceptionStackTrace = e.StackTrace
                    } |> publish "dead_letter" 
                    channel.BasicAck(ea.DeliveryTag,false)
                try
                    let msgText = 
                        Encoding.UTF8.GetString(ea.Body.ToArray())
                    try
                        let msg = 
                            msgText 
                            |> Json.deserialize<'a>
                        match msg |> handler with
                        Success ->
                            printfn "Message ack'ed"
                            channel.BasicAck(ea.DeliveryTag,false)
                        | Failure m ->
                            printfn "Message could not be processed (%s). %s" m msgText
                        | Excep e ->
                            e |> deadLetter msgText
                    with e ->
                        e |> deadLetter msgText 
                        eprintfn "Failed to parse message (%s) (Message will be ack'ed). Error: %s %s" msgText e.Message e.StackTrace 
                with e ->
                   eprintfn  "Failed while processing message. %s %s" e.Message e.StackTrace
                   e |> deadLetter null
            ))
            
            channel.BasicConsume(queueName,false,consumer) |> ignore
            printfn "Watching queue: %s" queueName
            while keepAlive do
                System.Threading.Thread.Sleep(60000)
         with e ->
           eprintfn "Failed to subscribe to the queue. %s:%d. Message: %s" host port e.Message
           keepAlive <- false

   

    type Broker() =
        do
            let channel = init()
            declare channel "dead_letter"
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
            printfn "Publishing (%s) as generic on (%s)" msg queueName
            publishString queueName msg
