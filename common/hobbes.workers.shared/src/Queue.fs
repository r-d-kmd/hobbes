namespace Hobbes.Workers.Shared
open RabbitMQ.Client
open RabbitMQ.Client.Events
open System
open System.Text
open Hobbes.Helpers.Environment
open Hobbes.Web
open Hobbes.Web.RawdataTypes

module Queue = 
    let private factory = ConnectionFactory()
    let private connection = factory.CreateConnection()
    let private channel = connection.CreateModel()
    let private user = 
        match env "USER" null with
        null -> failwith "'USER' not configured"
        | u -> u
    let private password =
        match env "PASSWORD" null with
        null -> failwith "'PASSWORD' not configured"
        | p -> p
    let private host = 
        match env "HOST" null with
        null -> failwith "Queue 'HOST' not configured"
        | h -> h
    let private port = 
        match env "PORT" null with
        null -> failwith "Post not specified"
        | p -> int p
    
    factory.HostName <- host
    factory.Port <- port
    factory.UserName <- user
    factory.Password <- password
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
                  | Generic s -> s

    let watch (queue:Queue) handler =
        channel.QueueDeclare(queue.Name,
                             true,
                             false,
                             false,
                             null) |> ignore

        let consumer = EventingBasicConsumer(channel)
        consumer.Received.AddHandler(EventHandler<BasicDeliverEventArgs>(fun _ (ea : BasicDeliverEventArgs) ->
            let msg = Encoding.UTF8.GetString(ea.Body.ToArray())
            if handler msg then
                channel.BasicAck(ea.DeliveryTag,false)
        ))
        
        channel.BasicConsume(queue.Name,false,consumer) |> ignore

    let publish (queue:Queue) (message : string) = 
        channel.QueueDeclare(queue.Name, true, false, false, null) |> ignore
        
        let body = ReadOnlyMemory<byte>(Text.Encoding.UTF8.GetBytes(message))
        let properties = channel.CreateBasicProperties()
        properties.Persistent <- true

        channel.BasicPublish("",queue.Name, false,properties,body)