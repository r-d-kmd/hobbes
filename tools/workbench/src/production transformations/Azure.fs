namespace Workbench.Transformations
open Workbench.Types

module Azure = 

    open Hobbes.DSL
    open General
        
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
        ] |> createTransformation "stateRenaming"