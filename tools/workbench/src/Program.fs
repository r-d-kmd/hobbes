namespace Workbench

open Argu
open FSharp.Data
open Hobbes.Web 
open Hobbes.Helpers
open Hobbes.Helpers.Environment

type CLIArguments =
    Collection of Collection
    | Host of string
    | Pat of string
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            Collection _ -> "Collection of configurations to publish"
            | Host _ -> "The host to publish transformation and configurations to"
            | Pat _ -> "Master key or PAT to hobbes gateway"



module Program = 
    [<EntryPoint>]
    let main args =
        let arguments = 
            try
                let parser = ArgumentParser.Create<CLIArguments>(programName = "workbench")
                let arguments = parser.Parse args
                Some arguments
            with e -> 
                Log.excf e "Failed"
                None

        match arguments with
        None -> 0
        | Some arguments ->
            
            let collection = 
                match arguments.TryGetResult Collection with
                None -> 
                    match env "COLLECTION" null with
                    null -> Collection.Development
                    | collection ->
                        match collection.ToLower() with 
                        "production" -> Production
                        | "test" -> Test
                        | "dev" | "development" -> Development
                        | c -> failwithf "%s collection unknown" c
                | Some e -> e
                
            let host = 
                 match arguments.TryGetResult Host with
                 | Some host -> host
                 | _ -> 
                    match env "HOST" null with
                    null -> "http://gateway-svc"
                    | host -> host
            let pat = 
                 match arguments.TryGetResult Pat with
                 | Some pat -> pat
                 | _ -> 
                    match env "PAT" null with
                    null -> failwith "Pat must be provided"
                    | pat -> pat            
            
            Log.logf "Using host: %s" host
            let urlTransformations = host + "/admin/transformation"
            let urlConfigurations = host + "/admin/configuration"

            Workbench.Configurations.State.initialise()
            Workbench.Configurations.DevOps.initialise()
            Workbench.Configurations.Test.initialise()
            //Workbench.Configurations.MergeJoinTest.initialise()

            let transformations = 
                Workbench.Types.allTransformations collection
                |> Seq.map Newtonsoft.Json.JsonConvert.SerializeObject

            let configurations = 
                Workbench.Types.allConfigurations collection
                |> Seq.map (fun conf ->
                    let trans = 
                       let ts = conf.Transformations |> List.map(fun t -> sprintf "%A" t.Name)
                       System.String.Join(",", ts)
                    
                    sprintf """{
                            "_id" : "%s",
                            "transformations" : [%s],
                            "source" : %s
                        }""" conf.Name trans (conf.Source.ToString())
                )
            
            transformations 
            |> Seq.iter(fun doc ->
                Log.logf "Creating transformation: %s" (Database.CouchDoc.Parse doc).Id
                
                let res = 
                    Http.Request(urlTransformations, 
                                 httpMethod = "PUT",
                                 body = TextRequest doc,
                                 silentHttpErrors = true,
                                 headers = 
                                    [
                                       HttpRequestHeaders.BasicAuth pat ""
                                       HttpRequestHeaders.ContentType HttpContentTypes.Json
                                    ]
                                )
                if res.StatusCode > 300 || res.StatusCode < 200 then
                   Log.errorf "Failed to publish transformations. [%s]. %d - %s" url resp.StatusCode (resp |> Http.readBody)
            )

            configurations
            |> Seq.iter(fun doc ->
                Log.logf "Creating configurations: %s" (Database.CouchDoc.Parse doc).Id
                Http.Request(urlConfigurations, 
                    httpMethod = "PUT",
                    body = TextRequest doc,
                    headers = 
                       [
                          HttpRequestHeaders.BasicAuth pat ""
                          HttpRequestHeaders.ContentType HttpContentTypes.Json
                       ]
                ) |> ignore
            )
            0