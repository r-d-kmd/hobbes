#r "paket: //
nuget FSharp.Data //
nuget Fake ~> 5 //
nuget Fake.Core ~> 5 //
nuget Fake.Core.Target  //
nuget Fake.DotNet //
nuget Fake.DotNet.AssemblyInfoFile //
nuget Fake.DotNet.Cli //
nuget Fake.DotNet.NuGet //
nuget Fake.IO.FileSystem //
nuget Fake.Tools.Git ~> 5 //
nuget Thoth.Json.Net"
#load "Configuration.fsx"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open Fake.Core
open FSharp.Data
open Fake.Core.TargetOperators
open Thoth.Json.Net

let globalEnvFile = "env.JSON"
let env = Configuration.Environment.Environment(globalEnvFile)


let inline (<==) a b = 
    b ==> a


let createProcess silent command workingDir args =
    let arguments = 
        match args |> String.split ' ' with
        [""] -> Arguments.Empty
        | args -> args |> Arguments.OfArgs
    let proc = 
        RawCommand (command, arguments)
        |> CreateProcess.fromCommand
        |> CreateProcess.withWorkingDirectory workingDir
        |> CreateProcess.ensureExitCode
    if not silent then
        proc
        |> CreateProcess.withStandardOutput Inherit
    else
        proc

let run silent command workingDir (args : string) =
    let res = 
        createProcess silent command workingDir <| System.String.Join(" ",args.Split([|" "|], System.StringSplitOptions.RemoveEmptyEntries))    
        |> Proc.run

    res.ExitCode

let start silent command workingDir args =
    createProcess silent command workingDir args    
    |> Proc.start

let request httpMethod user pwd url =
    FSharp.Data.Http.RequestString(url,
        httpMethod = httpMethod,
        silentHttpErrors = true,
        headers = [HttpRequestHeaders.BasicAuth user pwd]
    )

type Data = JsonProvider<"""testdata.json""">

let get url =
    let masterkey = env.MasterUser
    let res = 
        url
        |> request "get" masterkey ""
    printfn "Data:\n %s" res

    res
    |> Data.Parse

type DocList = JsonProvider<"""{
  "total_rows": 13,
  "offset": 0,
  "rows": [
    {
      "id": "2f7a2df81111bc757fb3cd98539fc1ae",
      "key": "2f7a2df81111bc757fb3cd98539fc1ae",
      "value": {
        "rev": "1-ea3ceded3d66baaa8c97e6fa9b2feb98"
      }
    },
    {
      "id": "2f7a2df81111bc757fb3cd98539fc1ae:flowerpot.renaming",
      "key": "2f7a2df81111bc757fb3cd98539fc1ae:flowerpot.renaming",
      "value": {
        "rev": "1-59f8ab6945797d40df37c37e007f0bc7"
      }
    }
  ]
}""">

let getDns serviceName = 
    match Fake.Core.Environment.environVarOrNone "BUILD_ENV" with
    Some "docker" -> serviceName + "-svc"
    | _ -> "localhost"
let gateway_port = 
    match Fake.Core.Environment.environVarOrNone "BUILD_ENV" with
    Some "docker" -> 80
    | _ -> 8080
let gateway_dn = getDns "gateway"
let dbDn = getDns "db"

let listDocuments dbName =  
    let dbUser = env.CouchdbUser 
    let dbPwd = env.CouchdbPassword 
    let response = 
        sprintf "http://%s:5984/%s/_all_docs" dbDn dbName
        |> request "get" dbUser dbPwd 
    
    try 
        response
        |> DocList.Parse
    with _ ->
        eprintfn "Failed to parse %s" response
        reraise()
let kubectlf command args =
    start true "kubectl" "../kubernetes" (command + " " + args)
    |> ignore

let kubectl silent command args =
    run silent "kubectl" "../kubernetes" (command + " " + args)

let applyk dir = 
    run true "kubectl" dir "apply -k ."
    
let startJob silent jobName = 
    let kubectl = kubectl silent
    let iden = (sprintf "job.batch/%s" jobName)
    try
       kubectl "delete" iden |> ignore
    with _ ->
       () //will fail if there's nothing to delete

    kubectl "apply" (sprintf "-f %s-job.yaml" jobName) |> ignore
    try
        kubectl "wait" (sprintf "--for=condition=running --timeout=3s job/%s" jobName) |> ignore
    with _ -> () //just making sure the pod has been created. Essentially just a sleep 3
    if not silent then
        kubectlf "logs" ("-f " + iden) |> ignore
    jobName

let awaitJobCompletion timeout jobName = 
    kubectl true "wait" (sprintf "--for=condition=complete --timeout=%ds job/%s" timeout jobName) |> ignore
    jobName

type DockerCommand =
    Build
    | Push
    override x.ToString() =
        match x with
        Build -> "build"
        | Push -> "push"

let docker (command : DockerCommand) workingDir arguments =
    run false "docker" workingDir (command.ToString() + " " + arguments)

Target.create "All" ignore

let create name f =
    Target.create name f
    name ==> "All" |> ignore
    name

create "build" (fun _ ->
    docker Build "." "-t tester ." |> ignore
)

create "ping" (fun _ ->
    printfn "pong"
)

create "deploy" (fun _ ->
    if System.IO.File.Exists globalEnvFile then
        printfn "Using env file"
        run false "kubectl" ("apply -f " + globalEnvFile) |> ignore
    else
        printfn "Using env from var"
    
    if run false "kubectl" "../kubernetes/overlays/dev" "apply -k ." > 0 then 
        failwith "Failed applying all"
)

let awaitService serviceName =
    create ("await-" + serviceName) (fun _ ->
        try
            kubectl true "wait" (sprintf "--for=condition=ready pod -l app=%s --timeout=120s" serviceName) |> ignore
        with _ -> 
            //operation timed out
            kubectl false "describe" <| sprintf "pod -l app=%s" serviceName |> ignore
            kubectl false "logs" <| sprintf "service/%s-svc" serviceName |> ignore
            failwithf "Service %s didn't start" serviceName
    )

let forwardServicePort serviceName here there =
    create ("port-forward-" + serviceName) (fun _ ->
        try
            kubectlf "port-forward" (sprintf "service/%s-svc %d:%d" serviceName here there)
        with _ -> () 
    )

create "port-forwarding" (fun _ ->
   run false "sleep" "1" |> ignore
)

[
  "gateway", 8080, 80
  "db", 5984, 5984
  "uniformdata", 8099, 8085
  "configurations", 8089,8085
] |> List.iter(fun (serviceName, localPort, podPort) -> 
     let target = awaitService serviceName
     "deploy" ?=> target |> ignore
     target
         ==> forwardServicePort serviceName localPort podPort
         ==> "port-forwarding"
     |> ignore
)

create "publish" (fun t -> 
    t.Context.Arguments
    |> List.map(fun config -> 
        config,System.IO.Path.Combine("./transformations", config + ".hb")
    ) |> Seq.iter(fun (name,file) ->
        let url = sprintf "http://%s:%d/admin/configuration" gateway_dn gateway_port
        printfn "Uploading (%s) to: '%s'" file url
        let masterkey = env.MasterUser
        let response = 
            FSharp.Data.Http.Request(url,
                httpMethod = "PUT",
                headers = [HttpRequestHeaders.BasicAuth masterkey ""],
                silentHttpErrors = true,
                body = (Encode.object [
                                        "name", Encode.string name
                                        "hb", file
                                              |> System.IO.File.ReadAllText
                                              |> Encode.string
                                      ]
                        |> Encode.toString 0
                        |> TextRequest
                       )
            )
        let body = 
            match response.Body with
            | Binary b -> 
                let enc = 
                    match response.Headers |> Map.tryFind "Content-Type" with
                    None -> System.Text.Encoding.Default
                    | Some s ->
                        s.Split '=' 
                        |> Array.last
                        |> System.Text.Encoding.GetEncoding 
                enc.GetString b 
            | Text t -> t
        if response.StatusCode <> 200 then
            failwithf "Couln't upload configuration. %d %s" response.StatusCode body
    )
)

create "sync" (fun _ ->
    "sync"
    |> startJob true
    |> ignore
)

create "complete-sync" (fun _ ->
    let configs = (listDocuments "configurations").Rows |> Array.map(fun r -> r.Id.ToString())
    let configCount = configs.Length
    let mutable lastSeen = -1
    let countDataset lastSeen =
        let datasets = 
            (listDocuments "uniformcache").Rows
            |> Array.filter(fun r -> 
                configs
                |> Array.tryFind (fun c -> c = r.Id.ToString())
                |> Option.isSome
            )
        if datasets.Length <> lastSeen then
            printfn "Found %d datasets expecting %d" datasets.Length configCount
            
        datasets.Length
    
    while configCount > lastSeen do
        try 
            lastSeen <- countDataset lastSeen
            awaitJobCompletion 10 "sync" |> ignore 
            //the sync worker is done and the above exits immediately
            System.Threading.Thread.Sleep 10000
        with _ -> ()
)

let areEqual actual expected (successes,failed)=
    if actual = expected then
       (successes + 1), failed
    else
       eprintf "Expected %A but got %A\n" expected actual
       successes,(failed + 1)

create "data" (fun _ ->
    let res = 
        sprintf "http://%s:%d/data/json/flowerpot" gateway_dn gateway_port
        |> get

    let first = res.[0]

    let successes,failed = 
        (0,0)
        |> areEqual res.Length 27 
        //|> areEqual first.TimeStamp  (System.DateTime.Parse "17/03/2021 14:27:32")
        |> areEqual first.SprintName (Some "Iteration 3")
        |> areEqual first.WorkItemId  442401
        |> areEqual first.ChangedDate  (System.DateTime.Parse "30/04/2019 14:57:50")
        |> areEqual first.WorkItemType  "User Story"
        |> areEqual first.SprintNumber (Some 3)
        |> areEqual first.State  "Done"
    printfn "Successes: %d. Failures: %d" successes failed
    if failed > 0 then failwith "One or more tests failed"
)

Target.create "test" ignore
Target.create "retest" ignore
Target.create "setup-test" ignore
Target.create "publishAndSync" ignore

"retest"
   <== "port-forwarding"
   <== "publish"
   <== "sync"
   <== "test"
   
"publishAndSync"
    <== "publish"
    <== "sync"

"setup-test"
   <== "deploy"
   <== "port-forwarding"
   <== "publish"
   <== "sync"
   <== "complete-sync"

"build"
  ?=> "deploy"
  ?=> "port-forwarding"
  ?=> "publish"
  ?=> "sync"
  ?=> "data"

"sync"
  ?=> "complete-sync"

"port-forwarding"
  ?=> "complete-sync"

"test"
  <== "data"

Target.runOrDefaultWithArguments "all"
