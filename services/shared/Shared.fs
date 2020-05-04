namespace Hobbes.Shared

open FSharp.Data

module RawdataTypes =
    type internal CacheRecord = JsonProvider<"""{
            "_id" : "name",
            "timeStamp" : "24-09-2019",
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

module CommonFunctions =     
    let internal hash (input : string) =
            use md5Hash = System.Security.Cryptography.MD5.Create()
            let data = md5Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input))
            let sBuilder = System.Text.StringBuilder()
            (data
            |> Seq.fold(fun (sBuilder : System.Text.StringBuilder) d ->
                    sBuilder.Append(d.ToString("x2"))
            ) sBuilder).ToString()

    let internal key source = 
        source |> hash
     
    let internal createCacheRecord key data =
        //fail if the data is invalid in form
        data |> RawdataTypes.DataResult.Parse |> ignore
        
        let timeStamp = System.DateTime.Now.ToString (System.Globalization.CultureInfo.CurrentCulture)
        let record = 
            sprintf """{
                        "_id" : "%s",
                        "timeStamp" : "%s"
                        "data" : %s
                    }""" key
                         timeStamp
                         data

        let cacheRecord = record |> RawdataTypes.CacheRecord.Parse

        assert(cacheRecord.Id = key)
        assert(cacheRecord.TimeStamp = timeStamp)

        record