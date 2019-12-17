namespace Workbench.Transformations
open Hobbes.Parsing.AST

[<Workbench.Transformations(Workbench.Project.General)>]
module Metrics = 

    open Hobbes.DSL
    open General

    [<Workbench.Transformation 0>]
    let stateCountBySprint = 
        [
            only (SprintNumber.Expression |> isntMissing)
            pivot 
                  SprintNumber.Expression 
                  State.Expression 
                  Count 
                  WorkItemId.Expression
        ]
    
    [<Workbench.Transformation 1>]
    let simpleBurnUp =
        [
            //remove all other columns than those metioned
            slice columns [SprintNumber.Name; "Done"]
            //moving mean and expanding sum only make sense if we are sure we know the order
            sort by SprintNumber.Name
            //Create a column called Burn up thats the expanding sum ie running total of the done column
            create (column "Burn up") (expanding Sum (!> "Done")) 
            //Create a column named Velocity that's the moving mean of 'Done' of the last three rows
            create (column "Velocity") ((moving Mean 3 (!> "Done")))
        ]

    [<Workbench.Transformation 2>]
    let burnUpWithForecast =
        [
            //index the rows by sprint number
            index rows by SprintNumber.Expression
            sort by SprintNumber.Name
            //Create a column called Burn up Prediction thats a linear extrapolation ten rows ahead based on the last ten rows of the data set
            create (column "Burn up Prediction") ((linear extrapolationLimited) (!> "Burn up") 10 10)
            //drop the sprint number column (to recreate from the index with the new values from the extrapolation)
            slice columns [
                "Burn up"
                "Done"
                "Velocity"
                "Burn up Prediction"
            ]
            //required to populate the Sprint number column with the predicted values
            create SprintNumber.Name Keys
        ]
        