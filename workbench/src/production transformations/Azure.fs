namespace Workbench.Transformations

[<Workbench.Transformations(Workbench.Project.AzureDevOps)>]
module Azure = 

    open Hobbes.DSL

    [<Workbench.Transformation 0>]
    let stateRenaming = 
        [
            rename "State" "DetailedState"
            create (column  "State") (If (((!> "StateCategory") == (!!> "Completed")) .|| ((!> "StateCategory") == (!!> "Resolved")) .|| ((!> "StateCategory") == (!!> "Removed"))) (Then !!> "Done") (Else 
                                         (If (!> "StateCategory" == !!> "InProgress") (Then !!> "Doing") (Else !!> "Todo" ))
                                     ))
        ]