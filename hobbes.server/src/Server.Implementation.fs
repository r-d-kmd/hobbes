module Implementation

open Hobbes.FSharp.DataStructures
open Hobbes.Server.Db.Database
open Hobbes.Server.Db.Log
open Hobbes.Server.Db
open FSharp.Data
open Hobbes.Server.Security
open Routing

[<Get "/key/%s" >] 
let key token =
    let user = 
        token
        |> tryParseUser
        |> Option.bind(fun (user,token) -> 
              let userId = sprintf "org.couchdb.user:%s" user
              match users.TryGet userId with
              None  ->
                Log.logf  "Didn't find user. %s" userId
                let userRecord = 
                    sprintf """{
                        "_id" : "%s",
                      "name": "%s",
                      "type": "user",
                      "roles": [],
                      "password": "%s"
                    }""" userId user token
                userRecord
                |> users.Post
                |> ignore
                users.FilterByKeys [userId]
                |> Seq.head
                |> Some
              | s -> s 
        )

    match user with
    None ->
        eprintfn "No user token. Tried with %s" token 
        403,"Unauthorized"
    | Some (user) ->
        printfn "Creating api key for %s " user.Name
        let key = createToken user
        200,key

let private request user pwd httpMethod body url  =
    let headers =
        [
            HttpRequestHeaders.BasicAuth user pwd
            HttpRequestHeaders.ContentType HttpContentTypes.Json
        ]
    match body with
    None -> 
        Http.Request(url,
            httpMethod = httpMethod, 
            silentHttpErrors = true,
            headers = headers
        )
    | Some body ->
        Http.Request(url,
            httpMethod = httpMethod, 
            silentHttpErrors = true, 
            body = TextRequest body,
            headers = headers
        )

[<Get "/ping" >] 
let ping() = 
    let app = Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application
    
    200,sprintf """{"appVersion": "%s", "runtimeFramework" : "%s", "appName" : "%s"}""" app.ApplicationVersion app.RuntimeFramework.FullName app.ApplicationName

let delete databaseName =
    couch.Delete databaseName