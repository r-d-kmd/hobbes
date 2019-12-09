open Argu
open FSharp.Data
open Hobbes.Web
open Hobbes.Helpers

type Environment = 
    Development
    | Production

type CLIArguments =
    Tests
    | Publish
    | Sync of string
    | BackSync
    | Environment of Environment
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Tests -> "Flags that the tests should be run"
            | Publish -> "Publish the transformations to either development or production (set by environment)"
            | Sync _ -> "When sync-ing a project from azure"
            | Environment _ -> "Environment to publish transformations to"
            | BackSync _ -> "Used to sync fromproduction to development"

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

type RawdataKeyList = JsonProvider<"""{"rawdata" : ["_design/default","default_hash"]}""">
type ConfigurationsList = JsonProvider<"""{"configurations" : [{
  "_id": "_design/default",
  "_rev": "1-0a33cc75bce4cd13e58611618da4cc1d",
  "views": {
    "bySource": {
      "map": "function (doc) {\n             var srcproj = [doc.source,doc.dataset];\n emit(srcproj, doc._id);\n}"
    }
  },
  "language": "javascript"
},{
  "_id": "default_hash",
  "_rev": "1-49ed2bec0b6f1907f91f3937fcc94f4a",
  "hash": "ff87ad355ebd038ec8c78794f3278d9a"
}]}""">
type TransformationList = JsonProvider<"""{"transformations" : [{
  "_id": "Azure.stateRenaming",
  "_rev": "1-5f2b13ecbffefca7c484833cdd2e9e38",
  "lines": [
    "rename column \"State\" \"DetailedState\" ",
    "create column \"State\" ( if [  \"StateCategory\"  =  'Proposed' ] { 'Todo' } else { if [  \"StateCategory\"  =  'InProgress' ] { 'Doing' } else { 'Done' }})"
  ]
}]}""">
[<EntryPoint>]
let main args =
   
    let results = 
        try
            let parser = ArgumentParser.Create<CLIArguments>(programName = "workbench")
            let results = parser.Parse args
            Some results
        with e -> 
            printfn "%s" e.Message
            None

    match results with
    None -> 0
    | Some results ->
        let settingsFile = "workbench.json"
        let settings = 
            match results.TryGetResult Environment with
            None -> 
                
                if System.IO.File.Exists settingsFile then 
                    (settingsFile |> WorkbenchSettings.Load).Development
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
        let publish = results.TryGetResult Publish
        let backsync = results.TryGetResult BackSync
        let listTransformationsPath = "/admin/list/transformations"
        let listConfigPath = "/admin/list/configurations"
        let listRawdataPath = "/admin/list/rawdata"
        if backsync.IsSome && System.IO.File.Exists settingsFile then
            let settings = WorkbenchSettings.Load settingsFile
            let prod = settings.Production

            let rawKeys = 
                (prod.Host + listRawdataPath |> getString prod.Hobbes
                 |> RawdataKeyList.Parse).Rawdata

            let db = Database.Database("rawdata",ignore,Database.consoleLogger)
            rawKeys
            |> Array.iter(fun key ->
                let doc = 
                    prod.Host + "/admin/raw/" + key |> getString prod.Hobbes
                doc.Replace("_rev","prodRev") |> db.InsertOrUpdate |> ignore
            )

            let db = Database.Database("transformations",ignore,Database.consoleLogger)
            let configurations = 
                (prod.Host + listTransformationsPath |> getString prod.Hobbes
                 |> TransformationList.Parse).Transformations
                |> Array.map(fun doc -> doc.ToString().Replace("_rev","prodRev"))
            configurations
            |> Array.iter(db.InsertOrUpdate >> ignore)

            let db = Database.Database("configurations",ignore,Database.consoleLogger)
            let configurations = 
                (prod.Host + listConfigPath |> getString prod.Hobbes
                 |> ConfigurationsList.Parse).Configurations
                |> Array.map(fun doc -> doc.ToString().Replace("\"_rev\"","\"prodRev\""))
            configurations
            |> Array.iter(db.InsertOrUpdate >> ignore)
            0
        elif  test.IsSome || (sync.IsNone && publish.IsNone) then
            settings.Azure.TimePayrollKmddk |> Workbench.Tests.test|> ignore
            printfn "Press enter to exit..."
            System.Console.ReadLine().Length
        else            
            match sync with
            None -> 
                if publish |> Option.isSome then 
                    printfn "Using host: %s" settings.Host
                    let urlTransformations = settings.Host + "/admin/transformation"
                    let urlConfigurations = settings.Host + "/admin/configuration"
                    let urlClearCache = settings.Host + "/admin/clear/cache"

                    let pat = settings.Hobbes
                    let transformations = 
                        Workbench.Reflection.transformations()
                        |> Seq.map(fun (name,statements) ->
                            statements
                            |> List.map parse
                            |> ignore
                            
                            System.String.Join(",",
                                statements
                                |> List.map (fun stmt ->
                                   (stmt |> string).Replace("\\","\\\\\\\\").Replace("\"", "\\\"") |> sprintf "\n  %A"
                                )
                            ) |> sprintf "[%s\n]"
                            |> sprintf """{
                                "_id" : "%s",
                                "lines" : %s
                            }
                            """ name
                        )
                    let configurations = 
                        Workbench.Reflection.configurations()
                        |> Seq.map(fun c ->
                            sprintf """{
                                "_id" : "%s",
                                %s,
                                "transformations" : [%s]
                            }""" c.Id
                                 ((c.Source |> Workbench.Source.project ||| c.Project) |> Workbench.Project.configString)
                                 (System.String.Join(",",c.Transformations |> Seq.map(sprintf "%A")))
                        ) 
                    
                    transformations 
                    |> Seq.iter(fun doc ->
                        printfn "Creating transformation: %s" (Database.CouchDoc.Parse doc).Id
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
                           printfn "Failed to publish transformations. URL: %s Msg: %s" urlTransformations e.Message
                           reraise()
                    )

                    configurations
                    |> Seq.iter(fun doc ->
                        printfn "Creating configurations: %s" (Database.CouchDoc.Parse doc).Id
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

                    //clear the cache so that it'll be rebuild based on the new transformations
                    //TODO: only clear if transformation related to cache has changed
                    Http.Request(urlClearCache, 
                                     httpMethod = "GET",
                                     headers = 
                                        [
                                           HttpRequestHeaders.BasicAuth pat ""
                                        ]
                                    ) |> ignore
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