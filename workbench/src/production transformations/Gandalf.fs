module Gandalf

open Hobbes.DSL

let renaming = 
    [
        only (!> "WorkItemType" == !!> "User Story" .|| !> "WorkItemType" == !!> "Bug")
        rename "Iteration.IterationLevel4" "Sprint"
    ]