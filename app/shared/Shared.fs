namespace Hobbes.Shared

open FSharp.Data

module RawdataTypes =
    type internal CacheRecord = JsonProvider<"""{
            "_id" : "name",
            "timeStamp" : "24-09-2019",
            "searchKey" : "lækljk",
            "state" : "Sync state",
            "revision" : "lkjlkj",
            "data" : {
                "columnNames" : ["a","b"],
                "values" : [["zcv","lkj"],[1.2,3.45],["2019-01-01","2019-01-01"]]
            } 
        }""">
        
    type internal Config = JsonProvider<"""{
            "_id" : "name",
            "source" : "azuredevops",
            "searchKey" : "kmddk",
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

