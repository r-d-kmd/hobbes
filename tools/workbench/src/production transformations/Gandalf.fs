namespace Workbench.Transformations
open Workbench.Types

module Gandalf = 

    open Hobbes.DSL 
    open General
    
    let renaming = 
        [
            only ((WorkItemType.Expression == "User Story") .|| (WorkItemType.Expression == "Bug"))
            rename "Iteration.IterationLevel4" SprintName.Name
            create (column SprintNumber.Name) (int (regex (!> "Sprint Name") "[Ss][Pp][Rr][Ii][Nn][Tt] [^\\d]*([\\d]+).*" [``$1``]))
        ]  |> createTransformation "gandalf.renaming"      