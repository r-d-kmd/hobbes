namespace Hobbes.Server.Db

module DataConfiguration =

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

    let get configurationName =
        let record = 
            configurationName
            |> Database.configurations.Get
       
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