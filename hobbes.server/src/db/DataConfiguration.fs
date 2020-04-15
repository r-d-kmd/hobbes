namespace Hobbes.Server.Db

open FSharp.Data 
open Hobbes.Web.Log
open Hobbes.Web

module DataConfiguration =
    type internal Data = JsonProvider<""" {"columnNames" : ["a","b"], "rows" : [[0,"hk",null,2.,3,4,"2019-01.01"],[0.4,1.2,2.4,3.5,4.1],["x","y","z"],["2019-01.01","2019-01.01"]]} """>
    type internal ConfigurationRecord = JsonProvider<"""[{
          "_id": "flowerpot.State.onlyUserStories",
          "_rev": "5-b6433576152e3d1d8c7183499ce5b565",
          "source": "azure devops",
          "dataset": "flowerpot",
          "transformations": [
            "Flowerpot.renaming",
            "Azure.stateRenaming",
            "General.onlyUserStory"
          ]
        }, {
            "_id" : "name",
            "source" : "azuredevops",
            "account" : "kmddk",
            "project" : "gandalf",
            "transformations" : ["transformation 1", "transformation 2"],
            "subConfigs": ["Config1", "Config2"]
        },
        {
        "_id" : "name",
        "azureDevOps" : {
            "account" : "kmddk",
            "project" : "gandalf"
        },
        "transformations" : ["transformation 1", "transformation 2"],
        "subConfigs": ["Config1", "Config2"]
    },{
        "_id" : "name",
        "rallly" : {
            "project" : "gandalf"
        },
        "transformations" : ["transformation 1", "transformation 2"]
    },{
        "_id" : "name",
        "jira" : {
            "project" : "gandalf"
        },
        "transformations" : ["transformation 1", "transformation 2"]
    }]""", SampleIsList = true>
    let private sourceView = "bySource"
    let private db = 
        Database.Database("configurations", ConfigurationRecord.Parse, Log.loggerInstance) 
                 .AddView(sourceView)
    [<System.Obsolete>]
    type DataSource = 
        AzureDevOps of account: string * projectName: string
        | Rally of projectName : string
        | Jira of projectName : string
        | Test
        | Unsupported
        with member 
                x.ProjectName 
                    with get() =
                        match x with
                        AzureDevOps (_, projectName)
                        | Rally projectName
                        | Jira projectName -> projectName
                        | Test -> System.String.Empty
                        | Unsupported -> System.String.Empty
             member 
                x.SourceName 
                    with get() =
                        match x with
                        AzureDevOps _ -> "azure devops"
                        | Rally _ -> "rally"
                        | Jira  _ -> "jira"
                        | Test -> "test"
                        | Unsupported -> "Unsupported source"
             member this.ConfDoc 
                with get() =
                    //TODO: this match should be removed 
                    match this with
                    AzureDevOps(account,_) as s ->
                            sprintf """{
                                "source" : "%s",
                                "account" : "%s",
                                "project" : "%s"
                            }""" s.SourceName account s.ProjectName 
                    | _ -> 
                        failwith "This is deprecated"

    
    //TODO: Should be able to depend on other configuratiuons
    //with the result of the transformations of either to be joined together on the index
    //and potential transformations to be applied to the result of the join
    type Configuration = 
        {
            Source : DataSource
            Transformations : string list
            SubConfigs : string list
        }

    let store doc = 
       db.InsertOrUpdate doc

    let list() = 
        db.List()

    let configurationsBySource (source : DataSource) = 
        let startKey = 
            sprintf """["%s","%s"]""" (source.SourceName.ToLower()) (source.ProjectName.ToLower())
        db.Views.[sourceView].List((fun s -> s.Trim '\"'),
                                  startKey =  startKey)
        

    let get configurationName =
        if System.String.IsNullOrWhiteSpace configurationName then failwith "Must supply a configuration name"
        let record = 
            configurationName
            |> db.Get
        let source =
            match record.AzureDevOps with
            Some devops ->
                let account = 
                    if System.String.IsNullOrWhiteSpace devops.Account then 
                        "kmddk" 
                    else 
                        devops.Account
                AzureDevOps(account, devops.Project)
            | None ->
                AzureDevOps("kmddk",record.Dataset.Value)
        {
            Source = source
               
            Transformations = 
                record.Transformations 
                |> List.ofArray

            SubConfigs = 
                record.SubConfigs
                |> List.ofArray            
        }