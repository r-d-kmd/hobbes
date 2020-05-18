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
    let channel = connection.CreateModel()
    type MessageType = 
        Syncronization of Config.Source
        | CacheUpdated
    let formatMessage messageType body = 
        ""
    let watch handler =
        let user = 
            match env "USER" null with
            null -> failwith "'USER' not configured"
            | u -> u
        let password =
            match env "PASSWORD" null with
            null -> failwith "'PASSWORD' not configured"
            | p -> p
        let host = 
            match env "HOST" null with
            null -> failwith "Queue 'HOST' not configured"
            | h -> h
        let port = 
            match env "PORT" null with
            null -> failwith "Post not specified"
            | p -> int p

        let workerName = 
            match env "WORKER_NAME" null with
            null -> "'WORKER_NAME' not set"
            | w -> w
        
        
        factory.HostName <- host
        factory.Port <- port
        factory.UserName <- user
        factory.Password <- password
        let queueName = workerName

        channel.QueueDeclare(queueName,
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
        
        channel.BasicConsume(queueName,false,consumer) |> ignore