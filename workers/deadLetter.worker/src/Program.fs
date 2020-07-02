open Hobbes.Helpers.Environment
open Worker.Git.Reader
open Hobbes.Web
open Hobbes.Messaging.Broker
open Hobbes.Messaging
open Hobbes.Helpers
open Hobbes.Web.Cache

let handleMessage message =
    ()
    
    

[<EntryPoint>]
let main _ =
    Database.awaitDbServer()
    
    async{    
        do! awaitQueue()
        Broker.Git handleMessage
        assert(try [||] |> Array.length > 0 with _ -> false)
    } |> Async.RunSynchronously
    
    0