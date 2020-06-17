namespace Workbench.Transformations
open Workbench.Types

module EzEnergy = 

    open Hobbes.DSL
    open General
    
    let renaming = 
        [
            only ((WorkItemType.Expression == "User Story") .|| (WorkItemType.Expression == "Bug"))
            rename "Iteration.Name" SprintName.Name
            rename "FormattedID" WorkItemId.Name
            rename "_ValidFrom" ChangedDate.Name
            rename "Iteration.StartDate" SprintStartDate.Name
            rename "Iteration.EndDate" SprintEndDate.Name
            rename "Project.Name" Team.Name
            rename "PlanEstimate" Estimate.Name
            create (column State.Name) (If ((!> "ScheduleState") == (!!> "Accepted")) (Then !!> "Done") (Else 
                                          (If ((!> "StateCategory" == !!> "In-Progress") .|| (!> "StateCategory" == !!> "Completed")) (Then !!> "Todo") (Else !!> "Done" ))
                                       ))
                                    
        ]  |> createTransformation "ezenergy.renaming"