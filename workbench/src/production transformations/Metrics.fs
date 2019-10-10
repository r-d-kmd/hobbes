module Metrics


open Hobbes.DSL

let stateCountBySprint = 
    [
        pivot (!> "Sprint") (!> "State") Hobbes.Parsing.AST.Count (!> "WorkItemId")
    ]

let expandingCompletionBySprint =
    [
        slice columns ["Done"]
        create (column "Total Completed") (expanding Hobbes.Parsing.AST.Sum (!> "Done"))
        create (column "Sprint") Keys
    ]