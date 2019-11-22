namespace Workbench.Transformations

[<Workbench.Transformations(Workbench.Project.Delta)>]
module Delta = 

    open Hobbes.DSL

    [<Workbench.Transformation 0 >]
    let renaming = 
        [
            only ((!> "WorkItemType" == "User Story") .|| (!> "WorkItemType" == "Bug"))
            rename "Iteration.IterationLevel4" "Sprint"
        ]