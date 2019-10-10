module Tests
open Hobbes.Server.Db

let invalidJsonForGandalf() = 
    Implementation.data "1234" |> ignore

let test() = 
    let x = DataConfiguration.get "_all_docs"
    x