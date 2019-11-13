module Workbench.Tests
open Hobbes.Server.Db

let getData conf = 
    let statusCode,res = Implementation.csv conf
    res.Substring(0, min 500 res.Length) |> printf "Status: %d. %A" statusCode
    
let cacheInvalidation configName = 
    DataConfiguration.AzureDevOps configName //run these two lines of code to test cache invalidation
    |> Cache.invalidateCache

let sync configuration = 
    let _,key = Hobbes.Server.Readers.AzureDevOps.sync "y3cg7xrajppvd4b2wp6ahrgnsdkpf4sidtlinthcwepc2pjbzfuq" "gandalf" "abcd"
    printfn "sync key: %s" key

let thingy () =
    Collector.Implementation.initDb
    
let test() = 
    //Implementation.initDb() |> ignore
    //sync "flowerpot.State.stateBySprint" //"gandalf.State.expandingCompletionBySprint" 
    //getData (*"flowerpot.State.stateBySprint"*) "flowerpot.State.expandingCompletionBySprint"
    thingy() |> ignore
    