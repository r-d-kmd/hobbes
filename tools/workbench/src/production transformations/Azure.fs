namespace Workbench.Transformations
open Workbench.Types

module Azure = 

    open Hobbes.DSL
    open General
        
    let renaming = 
        [
            rename State.Name "DetailedState"
            create State.Name (If (contains (!> "StateCategory") [
                                      !!> "Completed"
                                      !!> "Resolved"
                                      !!> "Removed"
                                      ] )
                                (Then
                                    (!!> "Done") )
                                (Else
                                    ( 
                                      (If (!> "StateCategory" == !!> "InProgress") (Then !!> "Doing") (Else !!> "Todo" ))
                                    )))
            rename "Iteration.StartDate" "Sprint Start Date"
            rename "Iteration.EndDate" "Sprint End Date"
        ] |> createTransformation "stateRenaming"
    
    let uniformWorkItems =
       [
            only (!> "IsLastRevisionOfDay")
            slice Columns [
              "TimeStamp"
              "Sprint Name"
              "Iteration.EndDate"
              "Iteration.StartDate"
              "WorkItemId"
              "ChangedDate"
              "WorkItemType"
              "CreatedDate"
              "ClosedDate"
              "LeadTimeDays"
              "CycleTimeDays"
              "StoryPoints"
              "RevisedDate"
              "Priority"
              "Title"
              "Sprint Number"
              "State"
            ]
       ]|> createTransformation "uniformWorkItems"