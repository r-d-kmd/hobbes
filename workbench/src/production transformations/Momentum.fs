namespace Workbench.Transformations
open Hobbes.Parsing.AST

[<Workbench.Transformations(Workbench.Project.Momentum)>]
module Momentum = 

    open Hobbes.DSL
    open General
    
    [<Workbench.Transformation 0 >]
    let renaming = 
        [
            only ((WorkItemType.Expression == "User Story") .|| (WorkItemType.Expression == "Bug"))
            rename "Iteration.IterationLevel3" SprintName.Name
            create (column SprintNumber.Name) (int (regex (!> "Sprint Name") "[Ss][Pp][Rr][Ii][Nn][Tt] [^\\d]*([\\d]+).*" [``$1``]))
        ]

    
    [<Workbench.Transformation 1 >]
    let velocityMean = 
        [
            only ((WorkItemType.Expression == "User Story") .|| (WorkItemType.Expression == "Bug") .&& (State.Expression == "Closed"))
            rename "Iteration.IterationLevel3" SprintName.Name
            slice columns [SprintNumber.Name; "Done"]
            sort by SprintNumber.Name
            create (column "Velocity") ((moving Mean 3 (!> "Done")))
        ]