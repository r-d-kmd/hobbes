namespace Hobbes.Web

open FSharp.Data

module RawdataTypes =
   
    type TransformationRecord = JsonProvider<"""{"_id" : "jlk", "lines" : ["","jghkhj"]}""">

    type Config = JsonProvider<"""{
            "_id" : "name",
            "source" : {
                "name" : "azuredevops",
                "project" : "gandalf",
                "dataset" : "commits"
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
            config.Source |> keyFromSource
        with e ->
           failwithf "Failed to get key from (%s). Message: %s. Trace: %s" (config.JsonValue.ToString()) e.Message e.StackTrace
    
    let keyFromConfigDoc = 
        Config.Parse
        >> keyFromConfig