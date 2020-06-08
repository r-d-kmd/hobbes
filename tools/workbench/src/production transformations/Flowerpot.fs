namespace Workbench.Transformations
open Workbench.Types

module Flowerpot = 

    open Hobbes.DSL
    open General
    
    
    let renaming = 
        [
            only ((WorkItemType.Expression == "User Story") .|| (WorkItemType.Expression == "Bug"))
            rename "Iteration.IterationLevel2" SprintName.Name
            create (column SprintNumber.Name) (int (regex (!> "Sprint Name") "[Ii][Tt][Ee][Rr][Aa][Tt][Ii][Oo][Nn] [^\\d]*([\\d]+).*" [``$1``]))
        ] |> Transformation.Create "flowerpot.renaming"