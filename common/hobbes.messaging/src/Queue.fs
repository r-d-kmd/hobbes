namespace Hobbes.Workers.Shared
open RabbitMQ.Client
open RabbitMQ.Client.Events
open System
open System.Text
open Hobbes.Helpers.Environment

module Queue = 
    
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
    let init() =
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

    type Queue =
        Cache
        | AzureDevOps
        | Git
        | Generic of string
        with member queue.Name 
              with get() =
                  match queue with
                  Cache -> "cache"
                  | AzureDevOps -> "azuredevops"
                  | Git -> "git"
                  | Generic s -> s.ToLower().Replace(" ","")

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
        
    let watch (queue:Queue) handler (pause : int) =
        try
            let channel = init()
            channel.QueueDeclare(queue.Name,
                                 true,
                                 false,
                                 false,
                                 null) |> ignore

            let consumer = EventingBasicConsumer(channel)
            consumer.Received.AddHandler(EventHandler<BasicDeliverEventArgs>(fun _ (ea : BasicDeliverEventArgs) ->
                let msg = Encoding.UTF8.GetString(ea.Body.ToArray())
                if handler msg then
                    printfn "Message ack'ed"
                    channel.BasicAck(ea.DeliveryTag,false)
                else
                    printfn "Message could not be processed. %s" msg
            ))
            
            channel.BasicConsume(queue.Name,false,consumer) |> ignore
            printfn "Watching queue: %s" queue.Name
            while true do
                System.Threading.Thread.Sleep(pause)
         with e ->
           eprintfn "Failed to subscribe to the queue. %s:%d. Message: %s" host port e.Message
           reraise()

    let publish (queue:Queue) (message : string) = 
        try
            let channel = init()
            channel.QueueDeclare(queue.Name, true, false, false, null) |> ignore
            
            let body = ReadOnlyMemory<byte>(Text.Encoding.UTF8.GetBytes(message))
            let properties = channel.CreateBasicProperties()
            properties.Persistent <- true

            channel.BasicPublish("",queue.Name, false,properties,body)
            printfn "Message published to %s" queue.Name
        with e -> 
           eprintfn "Failed to publish to the queue. Message: %s" e.Message
           reraise()