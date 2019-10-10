module Metrics

open Hobbes.DSL

let stateCountBySprint = 
   [
        only (!> "WorkItemType" == !!> "User Story" .|| !> "WorkItemType" == !!> "Bug")
        rename "Iteration.IterationLevel4" "Sprint"
    ]
let expandingCompletionBySprint =
    [
        only (!> "WorkItemType" == !!> "User Story" .|| !> "WorkItemType" == !!> "Bug")
        rename "Iteration.IterationLevel4" "Sprint"
    ]