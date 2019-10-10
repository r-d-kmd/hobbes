module Azure

open Hobbes.DSL

let foldBySprint = 
    [
        group by ["Sprint"; "WorkItemId"] => ( maxby !> "ChangedDate")
        rename "State" "DetailedState"
        create (column  "State") (If ((!> "StateCategory") == (!!> "Proposed")) (Then !!> "Todo") (Else 
                                     (If (!> "StateCategory" == !!> "InProgress") (Then !!> "Doing") (Else !!> "Done" ))
                                 ))
    ]