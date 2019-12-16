namespace Workbench.Transformations

[<Workbench.Transformations(Workbench.Project.AzureDevOps)>]
module Azure = 

    open Hobbes.DSL
    open General

    [<Workbench.Transformation 0>]
    let userStories = 
        [
            only (WorkItemType.Expression == "User Story")
        ]

    [<Workbench.Transformation 0>]
    let onlyInSprint = 
        [
            only (SprintNumber.Expression |> isntMissing)
        ]
        
    [<Workbench.Transformation 1>]
    let stateRenaming = 
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
        ]