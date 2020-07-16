open Hobbes.Helpers.Environment
open Readers.AzureDevOps.Data
open Readers.AzureDevOps
open Hobbes.Web
open Hobbes.Messaging
open Hobbes.Messaging.Broker

let synchronize (source : AzureDevOpsSource.Root) token =
        try
            Reader.sync token source
            |> Some
        with e ->
            Log.excf e "Sync failed due to exception"
            None
        |> Option.bind(fun (statusCode,body) ->
            Log.debugf "Sync finised with statusCode %d and result %s" statusCode body
            if statusCode > 200 || statusCode < 300 then 
                match Reader.read source with
                None -> failwith "Could not read data from raw"
                | d -> d
            else
                Log.errorf  "Syncronization failed. %d Message: %s" statusCode body
                None                 
        )

let handleMessage message =
    match message with
    Empty -> Success
    | Sync sourceDoc -> 
        Log.debugf "Received message. %s" sourceDoc
        try
            let source = sourceDoc |> AzureDevOpsSource.Parse
            let token = 
                if System.String.IsNullOrWhiteSpace source.Server then 
                    if source.Account.ToString() = "kmddk" then
                        env "AZURE_TOKEN_KMDDK" null
                    else
                        env "AZURE_TOKEN_TIME_PAYROLL_KMDDK" null
                else 
                    if source.Server.ToString().Split("/").[3] = "kmddk" then
                        env "AZURE_TOKEN_KMDDK" null
                    else 
                        env "AZURE_TOKEN_TIME_PAYROLL_KMDDK" null

            match synchronize source token with
            None -> 
                sprintf "Conldn't syncronize. %s %s" sourceDoc token
                |> Failure 
            | Some (key,data) -> 
                let data = Cache.createCacheRecord key [] data
                match Http.post (Http.UniformData Http.Update) data with
                Http.Success _ -> 
                   Log.logf "Data uploaded to cache"
                   Success
                | Http.Error(status,msg) -> 
                    sprintf "Upload to uniform data failed. %d %s" status msg
                    |> Failure
        with e ->
            Log.excf e "Failed to process message"
            Excep e
    

[<EntryPoint>]
let main _ =
    Database.awaitDbServer()
    Database.initDatabases ["azure_devops_rawdata"]
    async{    
        do! awaitQueue()
        Broker.AzureDevOps handleMessage
    } |> Async.RunSynchronously
    0