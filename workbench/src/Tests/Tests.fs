module Tests
open Hobbes.Server.Db

let invalidJsonForGandalf() = 
    Implementation.data "1234" |> ignore

let testDbInit() =
    match Implementation.initDb() with
        200,_ -> 
            match Implementation.putDocument Database.transformations """{"_id" : "trans1", "lines" : ["only 1=1"]}""" with
            200,_ -> 
                Implementation.putDocument Database.configurations  """{"_id" : "conf1",
                                                                        "source" : "Azure DevOps",
                                                                        "dataset" : "flowerpot",
                                                                        "transformations" : ["trans1"]
                                                                    }"""
            | a -> a
        | a -> a
    |> printfn "Result: %A"
    
let testGetData() =
    Implementation.data "conf1"
    |> printf "%A"

let test() = 
    let x = DataConfiguration.get "_all_docs"
    x
