open System
open Hobbes.Helpers.Environment
open Readers.AzureDevOps.Data
open Hobbes.Web.RawdataTypes
open Readers.AzureDevOps
open Hobbes.Web
open Hobbes.Workers.Shared.Queue

let synchronize (source : AzureDevOpsSource.Root) token =
        try
            let statusCode, body = Reader.sync token source
            printfn "Sync finised with statusCode %d and result %s" statusCode body
            if statusCode > 200 || statusCode < 300 then 
                match Reader.read source with
                None -> failwith "Could not read data from raw"
                | d -> d
            else
                printfn "Syncronization failed. %d Message: %s" statusCode body
                None                 
        with e ->
            printfn "Sync failed due to exception: %s %s" e.Message e.StackTrace
            None

let handleMessage sourceDoc =
    printfn "Received message. %s" sourceDoc
    try
        let source = sourceDoc |> AzureDevOpsSource.Parse
        let token = 
            if source.Account.ToString() = "kmddk" then
                env "AZURE_TOKEN_KMDDK" null
            else
                env "AZURE_TOKEN_TIME_PAYROLL_KMDDK" null

        match synchronize source token with
        None -> 
            printfn "Conldn't syncronize. %s %s" sourceDoc token
            false
        | Some data -> 
            match Http.post (Http.UniformData Http.Update) id data with
            Http.Success _ -> 
               printfn "Data uploaded to cache"
               true
            | Http.Error(status,msg) -> 
                printfn "Upload to uniform data failed. %s" msg
                false
    with e ->
        printfn "Failed to process message. %s %s" e.Message e.StackTrace
        false
    

[<EntryPoint>]
let main _ =
    Database.awaitDbServer()
    Database.initDatabases ["azure_devops_rawdata"]
    watch Queue.AzureDevOps handleMessage 5000
    
    0