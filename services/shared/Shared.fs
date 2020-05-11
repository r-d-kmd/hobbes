namespace Hobbes.Shared

open FSharp.Data

module RawdataTypes =
   
    type internal TransformationRecord = JsonProvider<"""{"_id" : "jlk", "lines" : ["","jghkhj"]}""">

    type internal Config = JsonProvider<"""{
            "_id" : "name",
            "source" : {
                "name" : "azuredevops",
                "project" : "gandalf",
                "dataset" : "commits"
            },
            "transformations" : ["jlk","lkjlk"],
            "subconfigs" : ["jlk","lkjlk"]
        }""">

    [<Literal>]
    let internal DataResultString = 
        """{
             "columnNames" : ["kjkl","kjlkj"],
             "rows" : [["dsfsd","kjlk"],[2.0,1.3]],
             "rowCount" : 2
        }"""
    type internal DataResult = JsonProvider<DataResultString>

    let keyFromSource (source : Config.Source) = 
        source.JsonValue.ToString()
        |> Hobbes.Web.Cache.key

    let keyFromConfig (config : Config.Root) =
        try 
            config.Source |> keyFromSource
        with e ->
           failwithf "Failed to get key from (%s). Message: %s. Trace: %s" (config.JsonValue.ToString()) e.Message e.StackTrace
    
    let keyFromConfigDoc = 
        Config.Parse
        >> keyFromConfig