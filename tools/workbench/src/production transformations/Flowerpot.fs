namespace Workbench.Transformations

[<Workbench.Transformations(Workbench.Project.Flowerpot)>]
module Flowerpot = 

    open Hobbes.DSL
    open General
    
    [<Workbench.Transformation 0 >]
    let renaming = 
        [
            only ((WorkItemType.Expression == "User Story") .|| (WorkItemType.Expression == "Bug"))
            rename "Iteration.IterationLevel2" SprintName.Name
        ]