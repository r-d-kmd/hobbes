namespace Workbench.Transformations
open Hobbes.DSL
open Workbench.Types

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
        ] |> Transformation.Create "baseData"

    let foldBySprint = 
        [
            //group by the tuple sprint name and workitem id
            group by ([SprintName.Expression; WorkItemId.Expression]) => 
                 //keep the row in each group where the ChangedDate is the highest 
                 //Ie keep the latest change of the work item in that particular sprint
                ( maxby ChangedDate.Expression)
        ] |> Transformation.Create "foldBySprint"

    let onlyInSprint = 
        [
            only (SprintNumber.Expression |> isntMissing)
        ] |> Transformation.Create "onlyInSprint"

    let all = 
        [
            only (SprintNumber.Expression == SprintNumber.Expression)
        ] |> Transformation.Create "AllWorkItems"