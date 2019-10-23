[<Workbench.Transformations>]
module General

open Hobbes.DSL

[<Workbench.Transformation>]
let foldBySprint = 
    [
        group by ["Sprint"; "WorkItemId"] => ( maxby !> "ChangedDate")
    ]
[<Workbench.Transformation>]
let onlyUserStory =
    [
        only (!> "WorkItemType" == "User Story")
    ]