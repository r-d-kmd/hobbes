namespace Hobbes.Web

open FSharp.Data

module RawdataTypes =
    type Transformation = 
        {
            [<Newtonsoft.Json.JsonProperty("_id")>]
            Name : string
            Statements : string list
            Description : string
        }
    
    type Config = JsonProvider<"""[{
            "_id" : "name",
            "source" : {
                "provider" : "azuredevops",
                "id" : "lkjlkj", 
                "project" : "gandalf",
                "dataset" : "commits",
                "server" : "https://analytics.dev.azure.com/kmddk/flowerpot"
            },
            "transformations" : ["jlk","lkjlk"]
        }, {
            "_id" : "name",
            "datasets" : ["cache key for a data set","lkjlkjlk"]
        }, {
            "_id" : "name",
            "join" : 
                {
                    "left": "cache key for a data set",
                    "right" : "cache key for a data set",
                    "field" : "name of field to join on "
                },
            "transformations" : ["jlk","lkjlk"]
        }]""", SampleIsList = true>
        
    let keyFromSourceDoc (source : string) = 
        source
        |> Hobbes.Web.Cache.key
        
    let keyFromSource (source : Config.Source) = 
        source.JsonValue.ToString()
        |> keyFromSourceDoc
    
    let keyFromConfig (config : Config.Root) =
        try 
                config.Source
                |> Option.bind(fun source -> 
                    let id = source |> keyFromSource
                    System.String.Join(":",id::(config.Transformations |> List.ofSeq))
                    |> Some
                ) |> Option.orElse(
                    config.Join
                    |> Option.bind(fun join ->
                        System.String.Join(":",(join.Left::join.Right::join.Field::(config.Transformations |> Array.toList))) |> Some
                    )
                ) |> Option.orElse(
                    config.Datasets
                    |> (System.String.Concat >> Hobbes.Helpers.Environment.hash >> Some)
                ) |> Option.get
        with e ->
           failwithf "Failed to get key from (%s). Message: %s. Trace: %s" (config.JsonValue.ToString()) e.Message e.StackTrace
    
    let keyFromConfigDoc = 
        Config.Parse
        >> keyFromConfig