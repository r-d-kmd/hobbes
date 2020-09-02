namespace Workbench.Transformations
open Hobbes.Parsing.AST
open Hobbes.Parsing
open Workbench.Types

module Playground = 

    open Hobbes.DSL
    open General

    let foldBySprint = 
        [
            group by ([SprintName.Name; WorkItemId.Name]) => 
                ( maxby ChangedDate.Expression)
        ] |> createTransformation "foldBySprint"

    let foldByMonth = 
        [
            only (State.Expression == "Done")
            create (column "Year") (date format ChangedDate.Name AST.Year)
            create (column "Month") (date format ChangedDate.Name AST.Month) 
            //group by the tuple sprint name and workitem id
            group by (["Year";"Month";WorkItemId.Name]) => 
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
        ] |> createTransformation "foldByMonth"