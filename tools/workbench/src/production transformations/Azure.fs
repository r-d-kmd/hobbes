namespace Workbench.Transformations
open Workbench.Types

module Azure = 

    open Hobbes.DSL
    open General
        
    (*[<Workbench.Transformation 2>]
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
        ]*)

    let stateRenaming =
        [
            create "SimpleState" (If (contains State.Expression [
                                         !!> "Closed"
                                         !!> "Ready for release"
                                         ] )
                                (Then 
                                    (!!> "Done"))
                                (Else
                                    (!!> "NotDone"))
                                 )                                     
        ]|> Transformation.Create "stateRenaming"