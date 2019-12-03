namespace Workbench.Transformations

[<Workbench.Transformations(Workbench.Project.Gandalf)>]
module Gandalf = 

    open Hobbes.DSL open General
    
    [<Workbench.Transformation 0 >]
    let renaming = 
        [
            only ((WorkItemType.Expression == "User Story") .|| (WorkItemType.Expression == "Bug"))
            rename "Iteration.IterationLevel4" SprintName.Name
        ]