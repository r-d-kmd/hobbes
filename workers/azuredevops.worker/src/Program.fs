open RabbitMQ.Client
open RabbitMQ.Client.Events
open System
open System.Text
open Hobbes.Helpers.Environment
open Readers.AzureDevOps.Data
open Hobbes.Shared.RawdataTypes
open Readers.AzureDevOps
open Hobbes.Web

let factory = ConnectionFactory()
let connection = factory.CreateConnection()
let channel = connection.CreateModel()
[<Literal>]
let secondsAnHour = 3600
[<Literal>]
let millisecondsASecond = 1000
let result = ref 0
let synchronize (config : Config.Root) token =
        try
            let statusCode, body = Reader.sync token config
            printfn "Sync finised with statusCode %d and result %s" statusCode body
            if statusCode < 200 || statusCode >= 300 then 
                eprintfn "Syncronization failed. Message: %s" body
            Some body                 
        with e ->
            eprintfn "Sync failed due to exception: %s %s" e.Message e.StackTrace
            None

let handleMessage _ (ea : BasicDeliverEventArgs) =
    let confDoc = Encoding.UTF8.GetString(ea.Body.ToArray())
    let conf = parseConfiguration confDoc
    let source = conf.Source |> source2AzureSource
    let token = 
        if source.Account.ToString() = "kmddk" then
            env "AZURE_TOKEN_KMDDK" null
        else
            env "AZURE_TOKEN_TIME_PAYROLL_KMDDK" null

    match synchronize conf token with
    Some _ ->
        match Http.post (Http.UniformData Http.Update) id confDoc with
        Http.Success _ -> 
            result := (0 + !result)
            if !result = 0 then
                channel.BasicAck(
                       deliveryTag = ea.DeliveryTag,
                       multiple = false
                )
        | Http.Error(status,msg) -> 
            eprintfn "Upload to uniform data failed. %s" msg
            result := status
    | None -> 
        result := 1

[<EntryPoint>]
let main _ =
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

    channel.QueueDeclare(
                     queue = queueName,     
                     durable = true,
                     exclusive = false, 
                     autoDelete = false, 
                     arguments = null) |> ignore

    let consumer = EventingBasicConsumer(channel)
    consumer.Received.AddHandler(EventHandler<BasicDeliverEventArgs>(handleMessage))
    
    while true do
        channel.BasicConsume(queueName,false,consumer) |> ignore
        System.Threading.Thread.Sleep(1 * (*secondsAnHour *) millisecondsASecond)
    
    !result