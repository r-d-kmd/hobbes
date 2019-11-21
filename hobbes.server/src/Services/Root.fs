namespace Hobbes.Server.Services

open Hobbes.Server.Db.Database
open Hobbes.Server.Db.Log
open Hobbes.Server.Routing
open Hobbes.Server.Security

[<RouteArea ("/", false)>]
module Root =
    
    [<Get "/ping" >] 
    let ping() = 
        let app = Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application
        
        200,sprintf """{"appVersion": "%s", "runtimeFramework" : "%s", "appName" : "%s"}""" app.ApplicationVersion app.RuntimeFramework.FullName app.ApplicationName

    [<Get "/key/%s" >] 
    let key token =
        let user = 
            token
            |> tryParseUser
            |> Option.bind(fun (user,token) -> 
                  let userId = sprintf "org.couchdb.user:%s" user
                  match users.TryGet userId with
                  None  ->
                    logf  "Didn't find user. %s" userId
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
