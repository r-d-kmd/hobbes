[<Workbench.Transformations>]
module Gandalf

open Hobbes.DSL

[<Workbench.Transformation>]
let renaming = 
    [
        only ((!> "WorkItemType" == "User Story") .|| (!> "WorkItemType" == "Bug"))
        rename "Iteration.IterationLevel4" "Sprint"
    ]