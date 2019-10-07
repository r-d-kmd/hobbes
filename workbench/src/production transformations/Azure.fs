module Azure

open Hobbes.DSL

let foldBySprint = 
    [
        group by ["Sprint"; "WorkItemId"] => ( maxby !> "ChangedDate")
    ]