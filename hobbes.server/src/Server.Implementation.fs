module Implementation

open Hobbes.FSharp.DataStructures
open Database
open Hobbes.Server.Db
open FSharp.Data
open Hobbes.Server.Security

let invalidateCache (conf : DataConfiguration.Configuration) =
    let idSuffix = conf.Source.SourceName + ":" + conf.Source.ProjectName
    let rec aux id =
        function
        | x :: xs -> let newId = (id + ":" + x)
                     Cache.delete newId |> ignore
                     aux newId xs
        | []      -> ()
    aux idSuffix conf.Transformations 
    
    200, "Cache invalidated"

let data configurationName =
    let cacheKey = configurationName
    let data = 
        match cacheKey
              |> Cache.tryRetrieve with
        None ->
            printfn "Cache miss %s" cacheKey
            let configuration = DataConfiguration.get configurationName
            let datasetKey =
                [configuration.Source.SourceName;configuration.Source.ProjectName]
            let rawData = Rawdata.list datasetKey
             
            let transformations = 
                Transformations.load configuration.Transformations
                |> Array.fold(fun st t -> st @ [Hobbes.FSharp.Compile.expressions t.Lines]) []

            
            let nextData =  rawData
                            |> Seq.map(fun (columnName,values) -> 
                                               columnName, values.ToSeq()
                                               |> Seq.map(fun (i,v) -> Hobbes.Parsing.AST.KeyType.Create i, v)
                            ) |> DataMatrix.fromTable                                                                
           
                
            let saveIntermediateResultToCache data transLength =
                let transNames = configuration.Transformations
                let rec aux  data name counter =
                    match counter with
                    | _ when counter < transLength -> 
                        let nextData = data |> transformations.[counter]
                        let newName = (name + ":" + transNames.[counter])
                        async {
                            nextData.ToJson(Column)
                            |> Cache.store (System.String.Join(':', datasetKey) + newName)
                            |> ignore
                        } |> Async.Start
                        
                        aux (nextData) newName (counter + 1)
                    | _ -> data.ToJson(Column)
                aux data "" 0                                    
                
            saveIntermediateResultToCache nextData transformations.Length

        | Some data ->
            printfn "Cache hit %s" cacheKey
            let table = 
                [|data.ToString() 
                  |> TableView.Parse|]
                |> TableView.toTable
                |> Seq.map(fun (columnName,values) -> 
                        columnName, values.ToSeq()
                                     |> Seq.map(fun (i,v) -> Hobbes.Parsing.AST.KeyType.Create i, v)
                ) |> DataMatrix.fromTable
            table.ToJson(Row)
    200,data
       
let request user pwd httpMethod body url  =
    let headers =
        [
            HttpRequestHeaders.BasicAuth user pwd
            HttpRequestHeaders.ContentType HttpContentTypes.Json
        ]
    match body with
    None -> 
        Http.Request(url,
            httpMethod = httpMethod, 
            silentHttpErrors = true,
            headers = headers
        )
    | Some body ->
        Http.Request(url,
            httpMethod = httpMethod, 
            silentHttpErrors = true, 
            body = TextRequest body,
            headers = headers
        )
let azureFields = 
    [
     "ChangedDate"
     "WorkITemId"
     "WorkItemRevisionSK"
     "WorkItemType"
     "State"
     "StateCategory"
     "LeadTimeDays"
     "CycleTimeDays"
     "Iteration"
    ]
let private clearTempAzureDataAndGetInitialUrl (source : DataConfiguration.DataSource) =
    let initialUrl = 
        let selectedFields = 
           (",", azureFields) |> System.String.Join
        sprintf "https://analytics.dev.azure.com/kmddk/%s/_odata/v2.0/WorkItemRevisions?$expand=Iteration&$select=%s&$filter=IsLastRevisionOfDay%%20eq%%20true%%20and%%20WorkItemRevisionSK%%20gt%%20%d" source.ProjectName selectedFields
    
    let latestId = 
        [source.SourceName;source.ProjectName]
        |> Rawdata.tryLatestId
    match latestId with
    Some workItemRevisionId -> 
        initialUrl workItemRevisionId
    | None -> initialUrl 0


let sync pat configurationName =
    let configuration = DataConfiguration.get configurationName
 

    match configuration.Source with
    DataConfiguration.AzureDevOps projectName ->
        
        let rec _sync (url : string) = 
            let resp = 
                url
                |> request pat pat "GET" None
            if resp.StatusCode = 200 then
                let record = 
                    match resp.Body with
                    Text body ->
                        body
                        |> AzureDevOpsAnalyticsRecord.Parse
                        |> Some
                    | _ -> 
                        None
                match record with
                Some record ->
                    let data = record.JsonValue.ToString JsonSaveOptions.DisableFormatting
                    let responseText = Rawdata.store configuration.Source.SourceName configuration.Source.ProjectName url data
                    if System.String.IsNullOrWhiteSpace(record.OdataNextLink) |> not then
                        printfn "Countinuing sync"
                        printfn "%s" record.OdataNextLink
                        _sync record.OdataNextLink
                    else 
                        200, responseText
                | None -> 500, "Couldn't parse record"
            else 
                resp.StatusCode, (match resp.Body with Text t -> t | _ -> "")
        let statusCode, body = 
            projectName
            |> DataConfiguration.AzureDevOps
            |> clearTempAzureDataAndGetInitialUrl
            |> _sync
        async {
            data configurationName |> ignore
        } |> Async.Start
        statusCode, body
    | _ -> 
        404,"No reader found" 
    

let key token =
    let user = 
        token
        |> tryParseUser
        |> Option.bind(fun (user,token) -> 
              let userId = sprintf "org.couchdb.user%%3A%s" user
              match users.TryGet userId with
              None ->
                printfn "Didn't find user. %s" userId
                let userRecord = 
                    sprintf """{
                      "name": "%s",
                      "type": "user",
                      "roles": [],
                      "password": "%s"
                    }""" user token
                (userId, userRecord)
                |> users.Put
                |> ignore
                users.Get userId
                |> Some
              | s -> s
        )

    match user with
    None ->
        eprintfn "No user token. Tried with %s" token 
        403,"Unauthorized"
    | Some (user) ->
        printfn "Creating api key for %s" user.Name
        let key = createToken user
        200,key

let putDocument (db : Database<'a>) id body =
    let rev = db.TryGetRev id
    let res = if rev.IsNone then db.TryPut(id, body)
                            else db.TryPut(id, body, rev.Value)      
    res.StatusCode, ""

let uploadDesignDocument db body =
    putDocument db "_design/default" body |> fst
    

let initDb =
    let dbStatusCode = ["_replicator"; "_global_changes"; "_users"; "transformations"; "rawdata"; "configurations"]
                       |> List.map (fun n -> couch.TryPut(n, "").StatusCode)
                       |> List.tryFind (fun sc -> sc < 200 || (400 >= sc && sc <> 412))

    let designDocument = System.IO.File.ReadAllText "db\\design_documents\\design_rawdata.json"
    let designStatusCode = uploadDesignDocument rawdata designDocument         
    if dbStatusCode.IsNone then designStatusCode, "" else dbStatusCode.Value, ""          