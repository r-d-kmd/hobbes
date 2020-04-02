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
    //let _,key = Hobbes.Server.Services.Data.sync configuration azureToken
    //printfn "sync key: %s" key
    ()

let git projectName repoName =
    let repos = 
     [
         "MemberUI", "gkghmibwqmtyfsekylepvwuwez44etts4wsdg4kunrzbmdrp32cq"
         "GandalfBackend", "stmwerd3yadmx7tmlbsg7ylf7y5grhyc7zhwu244oct2q2sb7zaq"
     ] |> Map.ofList 
    let remoteUrl = sprintf "https://kmddk.visualstudio.com/%s/_git/%s" projectName repoName
    //Collector.Git.Reader.sync "RSL" repos.[repoName] remoteUrl 
    let branches = Collector.Git.Reader.branches remoteUrl
    let commits = Collector.Git.Reader.commits remoteUrl
    printfn "%s" branches
    printfn "%s" commits

let test azureToken  = 
    //Implementation.initDb() |> ignore
    
    git "Gandalf" "GandalfBackend"
    //sync "delta.State.userStoriesFoldedBySprint" azureToken //"flowerpot.State.stateBySprint" //"gandalf.State.expandingCompletionBySprint" 
    //getData (*"flowerpot.State.stateBySprint"*) "flowerpot.State.expandingCompletionBySprint"
    