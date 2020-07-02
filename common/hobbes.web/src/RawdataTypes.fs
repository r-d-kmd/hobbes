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

    type Config = JsonProvider<"""{
            "_id" : "name",
            "source" : {
                "name" : "azuredevops",
                "project" : "gandalf",
                "dataset" : "commits",
                "server" : "https://analytics.dev.azure.com/kmddk/flowerpot"
            },
            "transformations" : ["jlk","lkjlk"],
            "subconfigs" : ["jlk","lkjlk"]
        }""">
        
    let keyFromSourceDoc (source : string) = 
        source
        |> Hobbes.Web.Cache.key
        
    let keyFromSource (source : Config.Source) = 
        source.JsonValue.ToString()
        |> keyFromSourceDoc
    
    let keyFromConfig (config : Config.Root) =
        try 
            let id = config.Source |> keyFromSource
            System.String.Join(":",id::(config.Transformations |> List.ofSeq))
        with e ->
           failwithf "Failed to get key from (%s). Message: %s. Trace: %s" (config.JsonValue.ToString()) e.Message e.StackTrace
    
    let keyFromConfigDoc = 
        Config.Parse
        >> keyFromConfig