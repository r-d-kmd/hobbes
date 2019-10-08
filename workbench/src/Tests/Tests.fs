module Tests

let invalidJsonForGandalf() = 
    Implementation.data "gandalf_1" |> ignore

let test() = 
    invalidJsonForGandalf()