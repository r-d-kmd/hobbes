module Workbench.Tests
open Hobbes.Server.Db

let getData conf = 
    let statusCode,res = Implementation.data conf
    res.Substring(0, min 500 res.Length) |> printf "Status: %d. %A" statusCode
    
let cacheInvalidation configName = 
    DataConfiguration.AzureDevOps configName //run these two lines of code to test cache invalidation
    |> Cache.invalidateCache

let test() = 
    //getData "1234" //run these two lines of code to test caching
    //getData "5678"
    
    cacheInvalidation "flowerpot"
    
    