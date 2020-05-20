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
            create (column SprintNumber.Name) (int (regex (!> "Sprint Name") "[Ss][Pp][Rr][Ii][Nn][Tt] [^\\d]*([\\d]+).*" [``$1``]))
        ]

    [<Workbench.Transformation 1>]
    let onlyInSprint = 
        [
            only (SprintNumber.Expression |> isntMissing)
        ]    
    