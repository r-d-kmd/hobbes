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
        let x = FSharp.Data.Http.RequestString("http://db-svc:5984/")
        200,sprintf """{"appVersion": "%s", "runtimeFramework" : "%s", "appName" : "%s", "db" : "%s"}""" app.ApplicationVersion app.RuntimeFramework.FullName app.ApplicationName x
    type UserSpec = FSharp.Data.JsonProvider<"""{"name" : "kjlkj", "token" : "lkælk"}""">

    [<Put ("/key", true) >] 
    let key userStr =
        printfn "User: %s" userStr
        let user = userStr |> UserSpec.Parse
        let verifiedUser = 
            let userIds = 
                users.ListIds() 
                |> Seq.filter(fun userId -> userId.StartsWith "org.couchdb.user:") 
                |> List.ofSeq
                
            printfn "Users in system: %A" userIds
            let isFirstUser = userIds |> List.isEmpty && System.String.IsNullOrWhiteSpace(user.Token)
            if isFirstUser ||  verifyAuthToken user.Token then
                let token = 
                   let rndstring = randomString 16
                   ( env "KEY_SUFFIX" (randomString 16)) + user.Name + rndstring |> hash
                
                let userId = sprintf "org.couchdb.user:%s" user.Name
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
                      }""" userId user.Name token

                  userRecord
                  |> users.InsertOrUpdate
                  |> ignore
                  users.FilterByKeys [userId]
                  |> Seq.head
                  |> Some
                | s -> s 
            else
                None        

        match verifiedUser with
        None ->
            errorf "" "No user token. Tried with %s" user.Token 
            403,"Unauthorized"
        | Some (user) ->
            printfn "Creating api key for %s " user.Name
            let key = createToken user
            200,key
