namespace Workbench.Transformations
open Workbench.Types

module Git =

    open Hobbes.Parsing.AST
    open Hobbes.Parsing
    open Hobbes.DSL

    let commitFrequency =
        [
            create (column "Date") (date format "Time" AST.Date)
            //group by the tuple sprint name and workitem id
            group by ["Date"] => 
                 //keep the row in each group where the ChangedDate is the highest 
                 //Ie keep the latest change of the work item in that particular sprint
                Count 
            //create (column "Commit frequency") (moving Mean 90)
            
        ] |> createTransformation "commit frequency"

    let allCommits = 
        [
            only True
        ] |> createTransformation "AllCommits"