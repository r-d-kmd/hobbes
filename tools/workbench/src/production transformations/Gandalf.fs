namespace Workbench.Transformations

[<Workbench.Transformations(Workbench.Project.Gandalf)>]
module Gandalf = 

    open Hobbes.DSL open General
    
    [<Workbench.Transformation 0 >]
    let renaming = 
        [
            only ((WorkItemType.Expression == "User Story") .|| (WorkItemType.Expression == "Bug"))
            rename "Iteration.IterationLevel4" SprintName.Name
            create (column SprintNumber.Name) (int (regex (!> "Sprint Name") "[Ss][Pp][Rr][Ii][Nn][Tt] [^\\d]*([\\d]+).*" [``$1``]))
        ]

    [<Workbench.Transformation 1>]
    let onlyInSprint = 
        [
            only (SprintNumber.Expression |> isntMissing)
        ]        