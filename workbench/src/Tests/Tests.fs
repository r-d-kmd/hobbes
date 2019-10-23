module Workbench.Tests
open Hobbes.Server.Db

let getData conf = 
    let statusCode,res = Implementation.csv conf
    res.Substring(0, min 500 res.Length) |> printf "Status: %d. %A" statusCode
    
let cacheInvalidation configName = 
    DataConfiguration.AzureDevOps configName //run these two lines of code to test cache invalidation
    |> Cache.invalidateCache

let sync configuration = 
    let _,key = Implementation.sync "y3cg7xrajppvd4b2wp6ahrgnsdkpf4sidtlinthcwepc2pjbzfuq" configuration
    let mutable state = Implementation.getSyncState key
    let mutable running = Cache.Started = state
    
    while running do
        printfn "Waiting for syncronization to complete"
        System.Threading.Thread.Sleep 5000
        state <-  Implementation.getSyncState key
        running <- Cache.Started = state

    if state = Cache.Synced then
        printfn "Syncronization completed succesfully"
        0
    else
        eprintfn "Syncronization failed"
        1

let generateKey token =
    Implementation.key token


let test() = 
    //Implementation.initDb() |> ignore
    //sync "gandalf" //"flowerpot" 
    generateKey "eyJFbWFpbCI6Imx1eF8tNEBob3RtYWlsLmNvbSIsIlVzZXIiOiJMdXhUaGVEdWRlIn0=%7C1571816785%7C5Cz1qRYApfqiHk3kn1lJjhYTio4="