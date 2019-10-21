namespace Hobbes.Server.Db

open FSharp.Data

module DataConfiguration =
    type private ConfigurationRecord = JsonProvider<"""{
        "_id" : "name",
        "source" : "name of source such as Azure DevOps, Rally or Jira",
        "dataset" : "name of the dataset. Eg a project name in azure devops",
        "transformations" : ["transformation 1", "transformation 2"]
    }""">
    let private sourceView = "bySource"
    let private db = 
        Database.Database("configurations", ConfigurationRecord.Parse)
                 .AddView(sourceView)
    type DataSource = 
        AzureDevOps of projectName: string
        | Rally of projectName : string
        | Jira of projectName : string
        | Test
        with member 
                x.ProjectName 
                    with get() =
                        match x with
                        AzureDevOps projectName
                        | Rally projectName
                        | Jira projectName -> projectName
                        | Test -> System.String.Empty
             member 
                x.SourceName 
                    with get() =
                        match x with
                        AzureDevOps _ -> "Azure DevOps"
                        | Rally _ -> "Rally"
                        | Jira  _ -> "Jira"
                        | Test  _ -> "Test"
                        
             static member Parse (source : string) project =
                project
                |> match source.ToLower() with
                   "azure devops" -> AzureDevOps
                   | "rally" -> Rally
                   | "jira" -> Jira
                   | _ -> fun _ -> Test
                

    type Configuration = 
        {
            Source : DataSource
            Transformations : string list
        }
    let store doc = 
       db.InsertOrUpdate doc

    let list() = 
        db.List()

    let configurationsBySource (source : DataSource) = 
        let startKey = 
            sprintf """["%s","%s"]""" source.SourceName source.ProjectName
        let endKey = 
            sprintf """["%s","%s_"]""" source.SourceName source.ProjectName
        db.Views.[sourceView].List(Database.CouchDoc.Parse, 
                                  startKey =  startKey, 
                                  endKey = endKey)
        |> Seq.map(fun c -> c.Id)

    let get configurationName =
        let record = 
            configurationName
            |> db.Get
       
        {
            Source = 
                record.Dataset
                |> (match record.Source.ToLower() with
                    "azure devops" | "azure" -> AzureDevOps
                    | "rally" -> Rally
                    | "jira" -> Jira
                    | _ -> failwithf "Couldn't read from source %s" record.Source)
            Transformations = 
                record.Transformations 
                |> List.ofArray
        }

    let tryGetRev id = db.TryGetRev id  
    let tryGetHash id = db.TryGetHash id  