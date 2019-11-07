[<Workbench.Transformations(Workbench.Project.AzureDevOps)>]
module Transformations.Azure

open Hobbes.DSL

[<Workbench.Transformation 0>]
let stateRenaming = 
    [
        rename "State" "DetailedState"
        create (column  "State") (If ((!> "StateCategory") == (!!> "Proposed")) (Then !!> "Todo") (Else 
                                     (If (!> "StateCategory" == !!> "InProgress") (Then !!> "Doing") (Else !!> "Done" ))
                                 ))
    ]