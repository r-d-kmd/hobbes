namespace Hobbes.Messaging

open RabbitMQ.Client
open RabbitMQ.Client.Events
open System
open System.Text

open Newtonsoft.Json

module Broker = 
    let inline private env name defaultValue = 
        match System.Environment.GetEnvironmentVariable name with
        null -> defaultValue
        | v -> v.Trim()
    let private serialize<'a> (o:'a) = JsonConvert.SerializeObject(o)
    let private deserialize<'a> json = JsonConvert.DeserializeObject<'a>(json)
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
            DependsOn : string
        }

    type MergeMessage =
        {
            CacheKey : string
            Datasets : string []
        }
     
    type JoinMessage = 
        {
            CacheKey : string
            Left : string
            Right : string
            Field : string
        }

    type Format = 
        Json

    type FormatMessage = 
        {
            CacheKey : string
            Format : Format
        }

    type CalculationMessage = 
        Transform of TransformMessage
        | Merge of MergeMessage
        | Join of JoinMessage
        | Format of FormatMessage

    type Message<'a> = 
        | Message of 'a

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
    let private watchDogInterval = 
        env "WATCH_DOG_INTERVAL" "1" |> int
    
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
            
    let awaitQueue() =
        let rec inner tries = 
            let retry() =  
                async{
                    do! Async.Sleep 5000
                    return! inner (tries + 1)
                }
            async{
                try
                    let channel = init()
                    declare channel "dead_letter"
                with e -> 
                    if tries % 1000 = 0 then
                        printfn "Queue not yet ready. Message: %s" e.Message
                    do! retry()
            } 
        inner 0

    type MessageResult = 
        Success
        | Failure of string
        | Excep of Exception

    let private publishString queueName (message : string) =
        try
            let channel = init()
            declare channel queueName
            
            let body = ReadOnlyMemory<byte>(Text.Encoding.UTF8.GetBytes(message))
            let properties = channel.CreateBasicProperties()
            properties.Persistent <- true

            channel.BasicPublish("",queueName, false,properties,body)
            printfn "Message (%s) published to %s:%d/%s" message host port queueName
        with e -> 
           eprintfn "Failed to publish to the queue. Message: %s" e.Message

    let private publish<'a> queueName (message : Message<'a>) =    
        message
        |> serialize
        |> publishString queueName
    
    let private watch<'a> queueName (handler : 'a -> MessageResult) =
        let mutable keepAlive = true
        let queue = Collections.Concurrent.ConcurrentQueue<'a>()
        try
            let channel = init()
            declare channel queueName

            let consumer = EventingBasicConsumer(channel)
            let deadLetter msgText (e : System.Exception) =
                    {
                        OriginalQueue = queueName
                        OriginalMessage = msgText
                        ExceptionMessage = e.Message
                        ExceptionStackTrace = e.StackTrace
                    } |> Message |> publish "dead_letter" 

            consumer.Received.AddHandler(EventHandler<BasicDeliverEventArgs>(fun _ (ea : BasicDeliverEventArgs) -> 
                let msgText = 
                    Encoding.UTF8.GetString(ea.Body.ToArray())
                try        
                    let msg = 
                        msgText
                        |> deserialize<Message<'a>>
                    printfn "msgtext: %s" msgText
                    match msg with
                    | Message msg ->
                        queue.Enqueue msg
                        channel.BasicAck(ea.DeliveryTag,false)
                with e ->
                    e |> deadLetter msgText
                    eprintfn "Failed to parse message (%s) (Message will be ack'ed). Error: %s %s" msgText e.Message e.StackTrace 
                
            ))
            
            channel.BasicConsume(queueName,false,consumer) |> ignore
            printfn "Watching queue: %s" queueName
            //to limit memory pressure, we're only going to handle one message at a time
            while keepAlive do
                match queue.TryDequeue() with
                true,msg ->
                    match msg |> handler with
                    Success ->
                        printfn "Message processed successfully"
                    | Failure m ->
                        printfn "Message could not be processed (%s). %s" m (serialize msg)
                    | Excep e ->
                        e |> deadLetter (serialize msg)
                | _ ->
                    System.Threading.Thread.Sleep(watchDogInterval / 2)
         with e ->
           eprintfn "Failed to subscribe to the queue. %s:%d. Message: %s" host port e.Message
           keepAlive <- false

    type Broker() =
        do
            let channel = init()
            declare channel "dead_letter"
        static member Cache(msg : CacheMessage) = 
            publish "cache" (Message msg)
        static member Cache (handler : CacheMessage -> _) = 
            watch "cache" handler
        static member AzureDevOps(msg : SyncMessage) = 
            publish "azuredevops" (Message msg)
        static member AzureDevOps (handler : SyncMessage -> _) = 
            watch "azuredevops" handler
        static member Git(msg : SyncMessage) = 
            publish "git" (Message msg)
        static member Git (handler : SyncMessage -> _) = 
            watch "git" handler
        static member Calculation(msg : CalculationMessage) = 
            publish "calculation" (Message msg)
        static member Calculation (handler : CalculationMessage -> _) = 
            watch "calculation" handler
        static member Generic queueName msg =
            assert(queueName |> String.IsNullOrWhiteSpace |> not)
            printfn "Publishing (%s) as generic on (%s)" msg queueName
            publishString queueName msg
