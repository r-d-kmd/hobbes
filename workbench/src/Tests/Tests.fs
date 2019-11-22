module Workbench.Tests
open Hobbes.Server.Db

let env name = 
    System.Environment.GetEnvironmentVariable name

let getData conf = 
    let _,res = Hobbes.Server.Services.Data.csv conf
    res.Substring(0, min 500 res.Length) |> printf "Csv: %A" 
    
let cacheInvalidation configName = 
    DataConfiguration.AzureDevOps configName //run these two lines of code to test cache invalidation
    |> Cache.invalidateCache

let sync configuration azureToken= 
    let _,key = Hobbes.Server.Services.Data.sync configuration azureToken
    printfn "sync key: %s" key
    
let test azureToken  = 
    //Implementation.initDb() |> ignore
    sync "delta.State.userStoriesFoldedBySprint" azureToken //"flowerpot.State.stateBySprint" //"gandalf.State.expandingCompletionBySprint" 
    //getData (*"flowerpot.State.stateBySprint"*) "flowerpot.State.expandingCompletionBySprint"
    