open System
open System.Text
open Hobbes.Helpers.Environment
open Readers.AzureDevOps.Data
open Hobbes.Web.RawdataTypes
open Readers.AzureDevOps
open Hobbes.Web

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
        match Http.post (Http.Configurations Http.Update) id confDoc with
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
    Hobbes.Workers.Shared.watch handleMessage
    printfn "Press enter to exit"
    let a = Console.ReadLine() 
    printfn "%s" a
    !result