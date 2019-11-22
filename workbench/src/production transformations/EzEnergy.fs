namespace Workbench.Transformations

[<Workbench.Transformations(Workbench.Project.EzEnergy)>]
module EzEnergy = 

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
            rename "PlanEstimate" "Story Points"
            create (column  "State") (If ((!> "ScheduleState") == (!!> "Accepted")) (Then !!> "Done") (Else 
                                         (If ((!> "StateCategory" == !!> "In-Progress") .|| (!> "StateCategory" == !!> "Completed")) (Then !!> "Todo") (Else !!> "Done" ))
                                     ))
                                    
        ]