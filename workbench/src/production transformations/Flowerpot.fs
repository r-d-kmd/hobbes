namespace Workbench.Transformations

[<Workbench.Transformations(Workbench.Project.Flowerpot)>]
module Flowerpot = 

    open Hobbes.DSL

    [<Workbench.Transformation 0 >]
    let renaming = 
        [
            only ((!> "WorkItemType" == "User Story") .|| (!> "WorkItemType" == "Bug"))
            rename "Iteration.IterationLevel2" "Sprint"
        ]