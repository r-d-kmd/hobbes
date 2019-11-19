[<Workbench.Transformations(Workbench.Project.EzEnergy)>]
module Transformations.EzEnergy

open Hobbes.DSL

[<Workbench.Transformation 0 >]
let renaming = 
    [
        only ((!> "WorkItemType" == "User Story") .|| (!> "WorkItemType" == "Bug"))
        rename "Iteration.Name" "Sprint"
        rename "FormattedID" "WorkItemId"
        rename "_ValidFrom" "ChangedDate"
        rename "Iteration.StartDate" "Sprint Start Date"
        rename "Iteration.EndDate" "Sprint End Date"
        rename "Project.Name" "Team"
        rename "Estimate" "Story Points"
        create (column  "State") (If ((!> "ScheduleState") == (!!> "Accepted")) (Then !!> "Done") (Else 
                                     (If ((!> "StateCategory" == !!> "In-Progress") .|| (!> "StateCategory" == !!> "Completed")) (Then !!> "Todo") (Else !!> "Done" ))
                                 ))
                                
    ]