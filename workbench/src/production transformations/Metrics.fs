namespace Workbench.Transformations
open Hobbes.Parsing.AST

[<Workbench.Transformations(Workbench.Project.General)>]
module Metrics = 

    open Hobbes.DSL
    open General

    [<Workbench.Transformation 0>]
    let stateCountBySprint = 
        [
            //Create a pivot table
            pivot 
                  //Use the sprint number as the row key
                  SprintNumber.Expression 
                  //Use the state column as column key
                  State.Expression 
                  //count the number of workitemids
                  Count WorkItemId.Expression
        ]
    
    [<Workbench.Transformation 1>]
    let expandingCompletionBySprint =
        [
            //remove all other columns than those metioned
            slice columns [SprintNumber.Name; "Done"]
            //index the rows by sprint number
            index rows by SprintNumber.Expression
            //sort by the sprint number column 
            sort by SprintNumber.Name
            //Create a column called Burn up thats the expanding sum ie running total of the done column
            create (column "Burn up") (expanding Sum (!> "Done")) 
            //Create a column named Velocity that's the moving mean of 'Done' of the last three rows
            create (column "Velocity") ((moving Mean 3 (!> "Done")))
            //Create a column called Burn up Prediction thats a linear extrapolation ten rows ahead based on the last ten rows of the data set
            create (column "Burn up Prediction") ((linear extrapolationLimited) (!> "Burn up") 10 10)
            //required to populate the Sprint number column with the predicted values
            index rows by SprintNumber.Expression
        ]