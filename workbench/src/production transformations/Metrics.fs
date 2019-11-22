namespace Workbench.Transformations

[<Workbench.Transformations(Workbench.Project.General)>]
module Metrics = 

    open Hobbes.DSL

    [<Workbench.Transformation 0>]
    let stateCountBySprint = 
        [
            pivot (!> "Sprint") (!> "State") Hobbes.Parsing.AST.Count (!> "WorkItemId")
            create (column "Sprint") Keys
        ]

    [<Workbench.Transformation 1>]
    let expandingCompletionBySprint =
        [
            slice columns ["Sprint";"Done"]
            sort by "Sprint" 
            create (column "Total Completed") (expanding Hobbes.Parsing.AST.Sum (!> "Done"))
            
        ]