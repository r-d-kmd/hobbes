module Tests
open Hobbes.Server.Db

let invalidJsonForGandalf() = 
    let configurationName = "gandalf.expandingCompletion" //"flowerpot"
    Implementation.data configurationName |> ignore

let testDbInit() =
    let intiRes = Implementation.initDb
    let transRes = Implementation.putDocument Database.transformations "trans1" """{"lines" : ["only 1=1"]}"""
    Implementation.putDocument Database.configurations "conf1" """{
                                                                                    "source" : "Azure DevOps",
                                                                                    "dataset" : "flowerpot",
                                                                                    "transformations" : ["trans1"]
                                                                                 }""" |> ignore

let testGetData() =
    Implementation.data "conf1"
    |> printf "%A"

let getAllConfigurations() = 
    let x = DataConfiguration.get "_all_docs"
    x |> ignore

let test() = 
    //getAllConfigurations()
    //testDbInit()
    invalidJsonForGandalf()
    //testGetData()
    
