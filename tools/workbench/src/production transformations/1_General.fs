namespace Workbench.Transformations
open Hobbes.DSL

[<Workbench.Transformations(Workbench.Project.General)>]
module General = 

    type ColumnName =
        SprintNumber
        | SprintName
        | SprintStartDate
        | SprintEndDate
        | WorkItemId
        | WorkItemType
        | Team
        | Estimate
        | ChangedDate
        | State
        with override x.ToString() = 
                match x with
                SprintNumber -> "Sprint Number"
                | SprintName -> "Sprint Name"
                | SprintStartDate -> "Sprint Start Date"
                | SprintEndDate -> "Sprint End Date"
                | WorkItemId -> "WorkItemId"
                | WorkItemType -> "WorkItemType"
                | Team -> "Team"
                | Estimate -> "Estimate"
                | ChangedDate -> "ChangedDate"
                | State -> "State"
             member x.Expression 
                with get() = 
                    Identifier (x.ToString())
             //same as tostring but easier to use in expressions
             member x.Name
                with get() = 
                    x.ToString()

    [<Workbench.Transformation 1>]
    //used to limit the amount of data that's kept in memory for calculations
    let baseData =
        [
            [
                SprintName
                SprintNumber
                SprintStartDate
                SprintEndDate
            ] |> List.map string
            |> slice columns 
        ] 

    [<Workbench.Transformation 0>]
    let foldBySprint = 
        [
            //group by the tuple sprint name and workitem id
            group by ([SprintName; WorkItemId]|> List.map string) => 
                 //keep the row in each group where the ChangedDate is the highest 
                 //Ie keep the latest change of the work item in that particular sprint
                ( maxby ChangedDate.Expression)
        ]