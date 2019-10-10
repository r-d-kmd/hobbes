module Tests
open Hobbes.Server.Db

let invalidJsonForGandalf() = 
    Implementation.data "1234" |> ignore

let test() = 
    Implementation.invalidateCache  (DataConfiguration.get "1234") |> ignore