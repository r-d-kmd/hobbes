namespace Workbench.Transformations

[<Workbench.Transformations(Workbench.Project.Delta)>]
module Delta = 

    open Hobbes.DSL
    open General

    [<Workbench.Transformation 0 >]
    let renaming = 
        [
            only (contains WorkItemType.Expression [
                                                     !!> "User Story"
                                                     !!> "Bug"
                                                   ])
            rename "Iteration.IterationLevel3" SprintName.Name
            create (column SprintNumber.Name) (regex (!> "Sprint Name") "[^\\d]*([\\d]+).*" [``$1``])
        ]