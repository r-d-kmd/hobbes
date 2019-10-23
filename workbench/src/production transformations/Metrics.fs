module Metrics


open Hobbes.DSL

let stateCountBySprint = 
    [
        pivot (!> "Sprint") (!> "State") Hobbes.Parsing.AST.Count (!> "WorkItemId")
        create (column "Sprint") Keys
    ]

let expandingCompletionBySprint =
    [
        slice columns ["Sprint";"Done"]
        create (column "Total Completed") (expanding Hobbes.Parsing.AST.Sum (!> "Done"))
        
    ]