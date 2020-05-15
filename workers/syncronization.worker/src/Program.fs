open RabbitMQ.Client
open RabbitMQ.Client.Events
open System.Text
open Hobbes.Helpers.Environment
open Hobbes.Shared.RawdataTypes
open Hobbes.Web

let factory = ConnectionFactory()
let connection = factory.CreateConnection()
let channel = connection.CreateModel()

[<Literal>]
let SecondsAnHour = 3600
[<Literal>]
let MillisecondsASecond = 1000

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

[<EntryPoint>]
let main _ =
    let queueName = ""
    let message = """{
          "name": "azure devops",
          "account": "kmddk",
          "project": "flowerpot"
        }"""


    channel.QueueDeclare(queueName, true, false, false, null) |> ignore
    
    let body = System.ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(message))
    let properties = channel.CreateBasicProperties()
    properties.Persistent <- true

    channel.BasicPublish("",queueName, properties,body)
    0