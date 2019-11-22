namespace Workbench.Transformations

[<Workbench.Transformations(Workbench.Project.General)>]
module General = 

    open Hobbes.DSL

    [<Workbench.Transformation 0>]
    let foldBySprint = 
        [
            group by ["Sprint"; "WorkItemId"] => ( maxby !> "ChangedDate")
        ]
    [<Workbench.Transformation 0>]
    let onlyUserStory =
        [
            only (!> "WorkItemType" == "User Story")
        ]