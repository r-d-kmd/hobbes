namespace Workbench.Transformations

[<Workbench.Transformations(Workbench.Project.AzureDevOps)>]
module Azure = 

    open Hobbes.DSL
    open General
    [<Workbench.Transformation 0>]
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