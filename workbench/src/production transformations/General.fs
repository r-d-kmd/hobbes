module General

open Hobbes.DSL

let foldBySprint = 
    [
        group by ["Sprint"; "WorkItemId"] => ( maxby !> "ChangedDate")
    ]

let onlyUserStory =
    [
        only (!> "WorkItemType" == "User Story")
    ]