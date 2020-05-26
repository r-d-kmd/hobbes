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
            Log.debugf "Sync finised with statusCode %d and result %s" statusCode body
            if statusCode > 200 || statusCode < 300 then 
                match Reader.read source with
                None -> failwith "Could not read data from raw"
                | d -> d
            else
                Log.errorf  "Syncronization failed. %d Message: %s" statusCode body
                None                 
        with e ->
            Log.excf e "Sync failed due to exception"
            None

let handleMessage sourceDoc =
    Log.debugf "Received message. %s" sourceDoc
    try
        let source = sourceDoc |> AzureDevOpsSource.Parse
        let token = 
            if source.Account.ToString() = "kmddk" then
                env "AZURE_TOKEN_KMDDK" null
            else
                env "AZURE_TOKEN_TIME_PAYROLL_KMDDK" null

        match synchronize source token with
        None -> 
            Log.errorf  "Conldn't syncronize. %s %s" sourceDoc token
            false
        | Some (key,data) -> 
            match Http.post (Http.UniformData Http.Update) id (sprintf """["%s",%s]""" key data) with
            Http.Success _ -> 
               Log.logf "Data uploaded to cache"
               true
            | Http.Error(status,msg) -> 
                Log.logf "Upload to uniform data failed. %d %s" status msg
                false
    with e ->
        Log.excf e "Failed to process message"
        false
    

[<EntryPoint>]
let main _ =
    Database.awaitDbServer()
    Database.initDatabases ["azure_devops_rawdata"]
    watch Queue.AzureDevOps handleMessage 5000
    
    0