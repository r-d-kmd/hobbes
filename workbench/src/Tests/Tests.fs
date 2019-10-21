module Workbench.Tests
open Hobbes.Server.Db

let getData conf = 
    let statusCode,res = Implementation.data conf
    res.Substring(0, min 500 res.Length) |> printf "Status: %d. %A" statusCode
    
let cacheInvalidation configName = 
    DataConfiguration.AzureDevOps configName //run these two lines of code to test cache invalidation
    |> Cache.invalidateCache

let sync configuration = 
    let _,key = Implementation.sync "y3cg7xrajppvd4b2wp6ahrgnsdkpf4sidtlinthcwepc2pjbzfuq" configuration
    let mutable running = Cache.Synced <> Implementation.getSyncState key
    while running do
        printfn "Waiting for syncronization to complete"
        System.Threading.Thread.Sleep 5000
        running <- Cache.Synced <> Implementation.getSyncState key

let test() = 
    //getData "1234" //run these two lines of code to test caching
    //getData "5678"
    
    //cacheInvalidation "flowerpot"

    Implementation.initDb() |> printfn "%A"
    sync "gandalf_1" 
    