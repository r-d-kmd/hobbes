namespace Workbench.Transformations
open Hobbes.Parsing.AST
open Hobbes.Parsing
open Workbench.Types

module Playground = 

    open Hobbes.DSL
    open General

    let foldBySprint = 
        [
            group by ([SprintName.Expression; WorkItemId.Expression]) => 
                ( maxby ChangedDate.Expression)
        ] |> Transformation.Create "foldBySprint"

    let foldByMonth = 
        [
            only (!> "SimpleState" == "Done")
            //group by the tuple sprint name and workitem id
            group by ([date format ChangedDate.Name AST.Year; date format ChangedDate.Name AST.Month;WorkItemId.Expression]) => 
                 //keep the row in each group where the ChangedDate is the highest 
                 //Ie keep the latest change of the work item in that particular sprint
                ( maxby ChangedDate.Expression)
            
            pivot 
                  //Use the sprint number as the row key
                  (date format ChangedDate.Name AST.Month)
                  //Use the state column as column key
                  //State.Expression 
                  (!> "WorkItemType")
                  //count the number of workitemids
                  Count WorkItemId.Expression
        ] |> Transformation.Create "foldByMonth"