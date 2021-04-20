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
#load "../build/Configuration.fsx"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open Fake.Core
open FSharp.Data
open Fake.Core.TargetOperators
open Thoth.Json.Net

let globalEnvFile = "../env.JSON"
let env = Configuration.Environment.Environment(globalEnvFile)

let masterkey = env.MasterUser
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

let run silent command workingDir args =
    let res = 
        createProcess silent command workingDir args    
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

let get =
    request "get" masterkey ""
    >> Data.Parse

let dbUser = env.CouchdbUser 
let dbPwd = env.CouchdbPassword 
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
let listDocuments =   
    sprintf "http://localhost:5984/%s/_all_docs"
    >> request "get" dbUser dbPwd 
    >> DocList.Parse

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
    kubectl false "wait" (sprintf "--for=condition=complete --timeout=%ds job/%s" timeout jobName) |> ignore
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
    run false "dotnet" ".." "fake build" |> ignore
)

create "deploy" (fun x ->
    let patch_dir =
        match x.Context.Arguments with
        "local"::[]-> "local_patches"
        | "prod"::[]  -> "prod_patches"
        | _       -> "local_patches"

    if System.IO.File.Exists globalEnvFile then
        printfn "Using env file"
        kubectl false "apply" ("-f " + globalEnvFile) |> ignore
    else
        printfn "Using env from var"

    let dirs = 
        System.IO.Directory.EnumerateDirectories("..", "kubernetes", System.IO.SearchOption.AllDirectories)
    
    dirs
    |> Seq.iter(fun dir ->
        let from = 
            (dir,patch_dir)
            ||> sprintf "%s/%s/kustomization.yaml"
        let ``to`` =
            dir
            |> sprintf "%s/kustomization.yaml"
        if System.IO.File.Exists from then
            System.IO.File.Copy(from , ``to``,true) 
    )
    
    let res = 
        dirs
        |> List.ofSeq
        |> List.sumBy(fun dir ->
            let kustomizationFilePath = 
                System.IO.Path.Combine(dir,"kustomization.yaml")
            let exists = 
               kustomizationFilePath
               |> Fake.IO.File.exists
            let res = 
                if exists then 
                    applyk dir
                else
                    System.IO.Directory.EnumerateFiles(dir,"*.yaml")
                    |> Seq.sumBy(fun file -> 
                        kubectl false "apply" (sprintf "-f %s" file)
                    )
            if res = 0 then
                let envFile = "localenv.yaml"
                if System.IO.Path.Combine(dir, envFile) |> System.IO.File.Exists then 
                    run true "kubectl" dir ("apply -f " + envFile)
                else
                    0
            else
                res

        )
    
    dirs
    |> Seq.iter(fun dir ->
        let filePath = System.IO.Path.Combine(dir,"kustomization.yaml")
        if System.IO.File.Exists filePath then
            System.IO.File.Delete(filePath) 
    )    

    if res > 0 then failwith "Failed applying all"
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

create "port-forwarding" ignore

[
  "gateway", 8080, 80
  "db", 5984, 5984
  "uniformdata", 8099, 8085
] |> List.iter(fun (serviceName, localPort, podPort) -> 
     let target = awaitService serviceName
     "deploy" ?=> target |> ignore
     target
         ==> forwardServicePort serviceName localPort podPort
         ==> "port-forwarding"
     |> ignore
)

create "publish" (fun _ ->    
    System.IO.Directory.EnumerateFiles("./transformations", "*.hb")
    |> Seq.iter(fun file ->
        let name = System.IO.Path.GetFileNameWithoutExtension file
        let url = sprintf "http://localhost:8080/admin/configuration"
        FSharp.Data.Http.Request(url,
            httpMethod = "PUT",
            headers = [HttpRequestHeaders.BasicAuth masterkey ""],
            body = (Encode.object [
                                    "name", Encode.string name
                                    "hb", file
                                          |> System.IO.File.ReadAllText
                                          |> Encode.string
                                  ]
                    |> Encode.toString 0
                    |> TextRequest
                   )
        ) |> ignore
    )
)

create "sync" (fun _ ->
    "sync"
    |> startJob true
    |> ignore
)

create "complete-sync" (fun _ ->
    let configCount = (listDocuments "configurations").TotalRows
    let countDataset() = 
        let count =
            (listDocuments "uniformcache").Rows
            |> Array.filter(fun r ->
                match r.Id.String
                    |> Option.map(fun str -> 
                        if str.EndsWith(":Json") then true
                        else false
                    ) with
                Some true -> true
                | _ -> false
            ) |> Array.length
        printfn "Found %d datasets expecting %d" count configCount
        count
    
    while configCount > countDataset() do
        try 
            awaitJobCompletion 10 "sync" |> ignore 
            //the sync worker is done and the above exits immediately
            System.Threading.Thread.Sleep 10000
        with _ -> ()
)

let areEqual actual expected (successes,failed)=
    if actual = expected then
       (successes + 1), failed
    else
       eprintf "Expected %A but got %A" expected actual
       successes,(failed + 1)

create "data" (fun _ ->
    let res = get "http://localhost:8080/data/json/azureDevops.Flowerpot.Test"
    let first = res.[0]

    let successes,failed = 
        (0,0)
        |> areEqual res.Length 27 
        |> areEqual first.TimeStamp  (System.DateTime.Parse "17/03/2021 14:27:32")
        |> areEqual first.SprintName None
        |> areEqual first.WorkItemId  79312
        |> areEqual first.ChangedDate  (System.DateTime.Parse "30/04/2019 14:57:50")
        |> areEqual first.WorkItemType  "User Story"
        |> areEqual first.SprintNumber None
        |> areEqual first.State  "Done"
    printfn "Successes: %d. Failures: %d" successes failed
    if failed > 0 then failwith "One or more tests failed"
)

Target.create "test" ignore
Target.create "retest" ignore
Target.create "setup-test" ignore

"retest"
   <== "port-forwarding"
   <== "publish"
   <== "sync"
   <== "test"
   

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

"port-forwarding"
  ==> "sync"

"sync"
  ?=> "complete-sync"

"port-forwarding"
  ==> "complete-sync"

"test"
  <== "data"

Target.runOrDefaultWithArguments "all"