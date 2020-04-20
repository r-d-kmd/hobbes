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
          "searchKey":"azure devopsflowerpot",
          "dataset": "flowerpot",
          "transformations": [
            "Flowerpot.renaming",
            "Azure.stateRenaming",
            "General.onlyUserStory"
          ]
        }, {
            "_id" : "name",
            "source" : "git",
            "searchKey":"azure devopsflowerpot",
            "account" : "kmddk",
            "project" : "gandalf",
            "transformations" : ["transformation 1", "transformation 2"],
            "subConfigs": ["Config1", "Config2"]
        }]""", SampleIsList = true>
    type Configuration = ConfigurationRecord.Root
    let parse doc = 
        let configuration = doc |> ConfigurationRecord.Parse
        assert(System.String.IsNullOrWhiteSpace(configuration.SearchKey) |> not)
        assert(System.String.IsNullOrWhiteSpace(configuration.Source) |> not)
        configuration

    let private sourceView = "bySource"
    let private db = 
        Database.Database("configurations", ConfigurationRecord.Parse, Log.loggerInstance) 
                 .AddView(sourceView)

    let store doc = 
       db.InsertOrUpdate doc

    let list() = 
        db.List()

    let configurationsBySource sourceKey = 
        db.Views.[sourceView].List((fun s -> s.Trim '\"'),
                                  startKey =  sourceKey)
        

    let get configurationName =
        if System.String.IsNullOrWhiteSpace configurationName then failwith "Must supply a configuration name"
        
        configurationName
        |> db.Get