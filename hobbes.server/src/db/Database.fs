module Database

open FSharp.Data

[<Literal>]
let private dbServerUrl = "http://db:5984/"

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
    "some column2" : ["rowValue1", "lars"]
}""">

type TransformationRecord = JsonProvider<"""{"lines" : ["","jghkhj"]}""">


type CacheRecord = JsonProvider<"""{
    "_id" : "name",
    "TimeStamp" : "24-09-2019",
    "Data" : "some json"
}""">


type List = JsonProvider<""" {"offset": 0,
    "rows": [
        {
            "id": "16e458537602f5ef2a710089dffd9453",
            "key": "16e458537602f5ef2a710089dffd9453",
            "value": {
                "rev": "1-967a00dff5e02add41819138abb3284d"
            }
        }]
    }""">

type private DatabaseName =
    Configurations
    | Transformations
    | Cache
    | RawData
    | Users
        
type Database<'a> (databaseName, parser : string -> 'a)  =
    
    let dbUrl = dbServerUrl + databaseName + "/"

    let urlWithId = 
        sprintf "%s%s/" dbUrl 
    member __.Get id = 
        Http.RequestString(urlWithId id)
        |> parser
    member __.TryGet id = 
        let resp = Http.Request(urlWithId id, silentHttpErrors = false)
        if resp.StatusCode = 200 then
            match resp.Body with
            Text s ->
             s |> parser |> Some
            | _ -> None
        else
            None
    member __.Put id body = 
        Http.RequestString(urlWithId id, httpMethod = "PUT", body = TextRequest body) |> ignore
    member __.List keys = 
        let body = 
           System.String.Join(",", 
               keys
               |> Seq.map(fun s -> sprintf "%A" s)
           )
           |> sprintf """{"keys" : [%s]}"""
        (Http.RequestString(dbUrl + "_all_docs",
                           httpMethod = "POST",
                           body = TextRequest body
        ) |> List.Parse).Rows
        |> Array.map(fun doc -> doc.ToString() |> parser)


let configurations = Database ("configurations", ConfigurationRecord.Parse)
let transformations = Database ("transformations", TransformationRecord.Parse)
let cache = Database ("cache", CacheRecord.Parse)
let rawdata = Database("rawdata", CacheRecord.Parse)
let users = Database ("_users", UserRecord.Parse)