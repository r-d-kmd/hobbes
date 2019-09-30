module Database

open FSharp.Data

let env name = 
    System.Environment.GetEnvironmentVariable name

let private dbServerUrl = 
    sprintf "http://%s:%s@db:5984"  (env "COUCHDB_USER") (env "COUCHDB_PASSWORD")

type UserRecord = JsonProvider<"""{
  "_id": "org.couchdb.user:dev",
  "_rev": "1-39b7182af5f4dc7a72d1782d808663b1",
  "name": "dev",
  "type": "user",
  "roles": [],
  "password_scheme": "pbkdf2",
  "iterations": 10,
  "derived_key": "492c5c2855d72ae88a5ff5f70f75ddb0ef63b32f",
  "salt": "a6212b3f03511523954fbc93e7c0907d"
}""">

type ConfigurationRecord = JsonProvider<"""{
    "_id" : "name",
    "source" : "name of source such as Azure DevOps, Rally or Jira",
    "dataset" : "name of the dataset. Eg a project name in azure devops",
    "transformations" : ["transformation 1", "transformation 2"]
}""">

type DataSet = JsonProvider<"""{
    "some column" : ["rowValue1", "rowValue2"],
    "some column2" : ["rowValue1", "rowValue2"]
}""">

type TransformationRecord = JsonProvider<"""{"lines" : ["","jghkhj"]}""">


type CacheRecord = JsonProvider<"""{
    "_id" : "name",
    "TimeStamp" : "24-09-2019",
    "Data" : "some json"
}""">


type List = JsonProvider<"""{
    "total_rows": 2,
    "offset": null,
    "rows": [
        {
            "id": "id1",
            "key": "id1",
            "value": {
                "rev": "1-a4f8ac9d02e65da8251583a0c893e26b"
            },
            "doc": {
                "_id": "id1",
                "_rev": "1-a4f8ac9d02e65da8251583a0c893e26b",
                "lines": [
                    "line1",
                    "line2",
                    "line3"
                ]
            }
        }]
    }""">
type TableView = JsonProvider<""" {"columnNames" : ["a","b"], "values" : [[0,1,2,3,4],[0.4,1.2,2.4,3.5,4.1],["x","y","z"],["2019-01.01","2019-01.01"]]} """>
type private DatabaseName =
    Configurations
    | Transformations
    | Cache
    | RawData
    | Users
        
type Database<'a> (databaseName, parser : string -> 'a)  =
    
    let dbUrl = dbServerUrl + "/" + databaseName

    let urlWithId (id : string) = 
        id
        |> sprintf "%s/%s" dbUrl
        
    member __.Get id =
        printfn "Requesting document %s from %s" id databaseName
        Http.RequestString(urlWithId id)
        |> parser
    member __.TryGet id = 
        let url = urlWithId id
        let resp = Http.Request(url, silentHttpErrors = true)
        if resp.StatusCode = 200 then
            match resp.Body with
            Text s ->
             s |> parser |> Some
            | _ -> None
        else
            printfn "Didn't find %s in %s. %s" id databaseName url
            None
    member __.Put id body = 
        Http.RequestString(urlWithId id, httpMethod = "PUT", body = TextRequest body) |> ignore
    member __.FilterByKeys keys = 
        let body = 
           System.String.Join(",", 
               keys
               |> Seq.map(fun s -> sprintf "%A" s)
           )
           |> sprintf """{"keys" : [%s]}"""
        (Http.RequestString(dbUrl + "/_all_docs?include_docs=true",
                           httpMethod = "POST",
                           body = TextRequest body
        ) |> List.Parse).Rows
        |> Array.map(fun entry -> entry.Doc.ToString() |> parser)
    member __.TableView key = 
        let url = sprintf """/_design/default/_view/table/?startkey=["%s"]&endkey=["%s"]""" key key
        (Http.RequestString(dbUrl + url,
                           httpMethod = "GET"
        ) |> List.Parse).Rows
        |> Array.map(fun entry -> entry.Doc.ToString() |> TableView.Parse)



let configurations = Database ("configurations", ConfigurationRecord.Parse)
let transformations = Database ("transformations", TransformationRecord.Parse)
let cache = Database ("cache", CacheRecord.Parse)
let rawdata = Database("rawdata", CacheRecord.Parse)
let users = Database ("_users", UserRecord.Parse)