open RabbitMQ.Client
open RabbitMQ.Client.Events
open System
open System.Text

let working = ref 0

(*let rec fibo i =
    match i with
    | 0 -> 1
    | 1 -> 1
    | x -> fibo(i-1) + fibo(i-2)*)

let handleMessage msg =
    Threading.Interlocked.Increment(working) |> ignore
    //fibo 46 |> ignore
    printfn "Recieved %s" msg
    Threading.Interlocked.Decrement(working) |> ignore

let decodeMessage (msg : ReadOnlyMemory<byte>) =
    Encoding.UTF8.GetString(msg.ToArray())

let init() =
    let factory = ConnectionFactory()
    factory.HostName <- "my-release-rabbitmq"
    factory.Port <- 5672
    factory.UserName <- "user"
    factory.Password <- "2ZLdDfjniG"
    let connection = factory.CreateConnection()
    let channel = connection.CreateModel()
    channel.QueueDeclare("hello", false, false, false, null) |> ignore

    let i = ref 0
    let count() =
        Threading.Interlocked.Increment(i) |> ignore

    let consumer = EventingBasicConsumer(channel)
    consumer.Received.AddHandler(EventHandler<BasicDeliverEventArgs>(fun _ ea ->
        let body = ea.Body
        let msg = decodeMessage body
        handleMessage msg
        count()
        ))

    channel.BasicConsume("hello", true, consumer) |> ignore

    let mutable lastI = 0
    let rec loop cont =
        if cont 
        then
            let wasEqual = if lastI = !i then Threading.Thread.Sleep 5000; true else Threading.Thread.Sleep 5000; lastI <- !i; false
            if lastI = !i && wasEqual then printfn "Idle exit"; loop false else loop cont
        else while !working <> 0 do ()
    loop true


[<EntryPoint>]
let main args =
    init()
    printfn "exited"
    0