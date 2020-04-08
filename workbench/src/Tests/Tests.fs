module Workbench.Tests

let env name = 
    System.Environment.GetEnvironmentVariable name

let sync configuration azureToken= 
    //let _,key = Hobbes.Server.Services.Data.sync configuration azureToken
    //printfn "sync key: %s" key
    ()

let test azureToken  = 
    //Implementation.initDb() |> ignore
    sync "delta.State.userStoriesFoldedBySprint" azureToken //"flowerpot.State.stateBySprint" //"gandalf.State.expandingCompletionBySprint" 
    //getData (*"flowerpot.State.stateBySprint"*) "flowerpot.State.expandingCompletionBySprint"
    