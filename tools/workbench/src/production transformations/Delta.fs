namespace Workbench.Transformations
open Workbench.Types

module Delta = 

    open Hobbes.DSL
    open General

    let renaming = 
        [
            only (contains WorkItemType.Expression [
                                                     !!> "User Story"
                                                     !!> "Bug"
                                                   ])
            rename "Iteration.IterationLevel3" SprintName.Name
            create (column SprintNumber.Name) (int (regex (!> "Sprint Name") "[Ss][Pp][Rr][Ii][Nn][Tt] [^\\d]*([\\d]+).*" [``$1``]))
        ] |> createTransformation "delta.renaming"