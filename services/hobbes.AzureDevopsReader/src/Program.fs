open RabbitMQ.Client
open RabbitMQ.Client.Events
open System
open System.Text
open Hobbes.Helpers.Environment
open Readers.AzureDevOps.Data

let result = ref 255
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

let handleMessage confDoc =
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
            result := 0
        | Http.Error(status,msg) -> 
            eprintfn "Upload to uniform data failed. %s" msg
            result := status
    | None -> 
        result := 1

let decodeMessage (msg : ReadOnlyMemory<byte>) =
    Encoding.UTF8.GetString(msg.ToArray())

let init() =
    let factory = ConnectionFactory()
    factory.HostName <- "my-release-rabbitmq"
    factory.Port <- 5672
    factory.UserName <- env "USER" null
    factory.Password <- env "PASSWORD" null
    let queueName = env "CHANNEL_NAME" "azuredevops"
    let connection = factory.CreateConnection()
    let channel = connection.CreateModel()
    channel.QueueDeclare(queueName, false, false, false, null) |> ignore

    let consumer = EventingBasicConsumer(channel)
    consumer.Received.AddHandler(EventHandler<BasicDeliverEventArgs>(fun _ ea ->
        let body = ea.Body
        let msg = decodeMessage body
        handleMessage msg
    ))

    channel.BasicConsume(queueName, true, consumer) |> ignore


[<EntryPoint>]
let main _ =
    init()
    !result