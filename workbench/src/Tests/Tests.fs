module Workbench.Tests
open Hobbes.Server.Db
open Hobbes.Server.Services

let env name = 
    System.Environment.GetEnvironmentVariable name

let getData conf = 
    let _,res = Hobbes.Server.Services.Data.csv conf
    res.Substring(0, min 500 res.Length) |> printf "Csv: %A" 
    
let cacheInvalidation configName = 
    DataConfiguration.AzureDevOps configName //run these two lines of code to test cache invalidation
    |> Cache.invalidateCache

let sync configuration = 
    let _,key = Hobbes.Server.Readers.AzureDevOps.sync (env "AZURE_TOKEN") ("kmddk","gandalf") "abcd"
    printfn "sync key: %s" key

let thingy () =
    Collector.Implementation.initDb
    
let test() = 
    //Implementation.initDb() |> ignore
    //sync "flowerpot.State.stateBySprint" //"gandalf.State.expandingCompletionBySprint" 
    //getData (*"flowerpot.State.stateBySprint"*) "flowerpot.State.expandingCompletionBySprint"
    thingy() |> ignore
    