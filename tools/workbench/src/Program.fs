open Argu
open FSharp.Data
open Hobbes.Web 
open Hobbes.Helpers

type Environment = 
    Development
    | Production

type CLIArguments =
    Tests
    | Publish of string
    | Sync of string
    | Environment of Environment
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Tests -> "Flags that the tests should be run"
            | Publish _ -> "Publish the transformations to either development or production (set by environment or given as arg (prod/dev) if using workbench.json)"
            | Sync _ -> "When sync-ing a project from azure"
            | Environment _ -> "Environment to publish transformations to"

let parse stmt =
    let stmt = stmt |> string
    Hobbes.Parsing.Parser.parse [stmt]
    |> Seq.exactlyOne

let getString pat url  = 
    Http.RequestString(url, 
                 httpMethod = "GET",
                 headers = 
                    [
                       HttpRequestHeaders.BasicAuth pat ""
                       HttpRequestHeaders.ContentType HttpContentTypes.Json
                    ]
    )
type ProcessResult = { exitCode : int; stdout : string; stderr : string }

let executeProcess (exe,cmdline) =
    let psi = System.Diagnostics.ProcessStartInfo(exe,cmdline) 
    psi.UseShellExecute <- false
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.CreateNoWindow <- true        
    let p = System.Diagnostics.Process.Start(psi) 
    let output = System.Text.StringBuilder()
    let error = System.Text.StringBuilder()
    p.OutputDataReceived.Add(fun args -> output.Append(args.Data) |> ignore)
    p.ErrorDataReceived.Add(fun args -> error.Append(args.Data) |> ignore)
    p.BeginErrorReadLine()
    p.BeginOutputReadLine()
    p.WaitForExit()
    { exitCode = p.ExitCode; stdout = output.ToString(); stderr = error.ToString() }

type WorkbenchSettings = FSharp.Data.JsonProvider<"""{
    "development" : {
        "azure" : {
            "kmddk" : "y3",
            "time-payroll-kmddk" : "g"
        },
        "hobbes" : "V",
        "host" : "http://"
    }, 
    "production": {
        "azure": {
            "kmddk": "4b",
            "time-payroll-kmddk": "gvr"
        }, 
        "hobbes": "VR",
        "host" : "https://"
    }
}""">

[<EntryPoint>]
let main args =
   
    let results = 
        try
            let parser = ArgumentParser.Create<CLIArguments>(programName = "workbench")
            let results = parser.Parse args
            Some results
        with e -> 
            Log.excf e "Failed"
            None

    match results with
    None -> 0
    | Some results ->
        let publish = results.TryGetResult Publish
        let settingsFile = "workbench.json"
        let settings = 
            match results.TryGetResult Environment with
            None -> 
                
                if System.IO.File.Exists settingsFile then 
                    match publish with 
                    | Some v when v.ToLower() = "prod" -> (settingsFile |> WorkbenchSettings.Load).Production
                    | Some v when v.ToLower() = "dev"  -> (settingsFile |> WorkbenchSettings.Load).Development
                    | _                                -> (settingsFile |> WorkbenchSettings.Load).Development
                    
                else
                     match env "WORKBENCH_ENVIRONMENT" null with
                     null -> failwith "No settings file and no env var"
                     | s -> 
                         (s 
                         |> WorkbenchSettings.Parse).Production
            | Some e -> 
               let settings = (settingsFile |> WorkbenchSettings.Load)
               match e with
               Development -> 
                   settings.Development
               | Production -> 
                   settings.Production
            
        let test = results.TryGetResult Tests 
        let sync = results.TryGetResult Sync
        if  test.IsSome || (sync.IsNone && publish.IsNone) then
            settings.Azure.TimePayrollKmddk |> Workbench.Tests.test|> ignore
            printfn "Press enter to exit..."
            System.Console.ReadLine().Length
        else            
            match sync with
            None -> 
                if publish |> Option.isSome then 
                    Log.logf "Using host: %s" settings.Host
                    let urlTransformations = settings.Host + "/admin/transformation"
                    let urlConfigurations = settings.Host + "/admin/configuration"

                    let pat = settings.Hobbes
                    Workbench.Configurations.State.initialise()
                    Workbench.Configurations.DevOps.initialise()
                    let transformations = 
                        Workbench.Types.allTransformations()
                        |> Seq.map Json.serialize

                    let configurations = 
                        Workbench.Types.allConfigurations()
                        |> Seq.map Json.serialize
                    
                    transformations 
                    |> Seq.iter(fun doc ->
                        Log.logf "Creating transformation: %s" (Database.CouchDoc.Parse doc).Id
                        printfn "DOCUMENT = %A" doc
                        try
                            Http.Request(urlTransformations, 
                                         httpMethod = "PUT",
                                         body = TextRequest doc,
                                         headers = 
                                            [
                                               HttpRequestHeaders.BasicAuth pat ""
                                               HttpRequestHeaders.ContentType HttpContentTypes.Json
                                            ]
                                        ) |> ignore
                        with e ->
                           Log.logf "Failed to publish transformations. URL: %s Msg: %s" urlTransformations e.Message
                           reraise()
                    )

                    configurations
                    |> Seq.iter(fun doc ->
                        Log.logf "Creating configurations: %s" (Database.CouchDoc.Parse doc).Id
                        Http.Request(urlConfigurations, 
                                     httpMethod = "PUT",
                                     body = TextRequest doc,
                                     headers = 
                                        [
                                           HttpRequestHeaders.BasicAuth pat ""
                                           HttpRequestHeaders.ContentType HttpContentTypes.Json
                                        ]
                                    ) |> ignore
                    )
                0
             | Some configurationName ->
                let pat = settings.Hobbes
                let url = settings.Host + "/data/sync/" + configurationName
                Http.Request(url, 
                                 httpMethod = "GET",
                                 headers = 
                                    [
                                       HttpRequestHeaders.BasicAuth pat ""
                                       HttpRequestHeaders.ContentType HttpContentTypes.Json
                                    ]
                                ) |> ignore
                0