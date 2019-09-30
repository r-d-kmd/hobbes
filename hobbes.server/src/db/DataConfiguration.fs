namespace Hobbes.Server.Db

module DataConfiguration =

    type DataSource = 
        AzureDevOps of projectName: string
        | Rally of projectName : string
        | Jira of projectName : string
        | TeamFoundationServer of serverUrl : string * projectName : string
        | Test
        with member 
                x.ProjectName 
                    with get() =
                        match x with
                        AzureDevOps projectName
                        | Rally projectName
                        | Jira projectName
                        | TeamFoundationServer(_,projectName) -> projectName
                        | Test -> System.String.Empty
             member 
                x.SourceName 
                    with get() =
                        match x with
                        AzureDevOps _ -> "AzureDevOps"
                        | Rally _ -> "Rally"
                        | Jira  _ -> "Jira"
                        | TeamFoundationServer _ -> "TFS"
                        | Test  _ -> "Test"
    type Configuration = 
        {
            Source : DataSource
            Transformations : string list
        }

    let get configurationName =
        let record = 
            configurationName
            |> Database.configurations.Get
        {
            Source = 
                record.Dataset
                |> (match record.Source.ToLower() with
                    "azure" -> AzureDevOps
                    | "rally" -> Rally
                    | "jira" -> Jira
                    | _ -> failwithf "Couldn't read from source %s" record.Source)
            Transformations = record.Transformations |> List.ofArray

        }     
        
        