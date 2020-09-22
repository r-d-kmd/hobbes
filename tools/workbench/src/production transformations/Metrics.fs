namespace Workbench.Transformations
open Hobbes.Parsing.AST
open Workbench.Types

module Metrics = 

    open Hobbes.DSL
    open General

    let stateCountBySprint = 
        [
            only (SprintNumber.Expression |> isntMissing)
            pivot 
                  //Use the sprint number as the row key
                  SprintNumber.Expression
                  //Use the state column as column key
                  //State.Expression 
                  (ColumnName.State.Expression)
                  //count the number of workitemids
                  Count WorkItemId.Expression
        ]  |> createTransformation "stateCountBySprint"

    let bugCountBySprint =
        [
            only (SprintNumber.Expression |> isntMissing)
            only ((contains WorkItemType.Expression [
                                      !!> "Bug"
                                      ] ))
            pivot 
                  //Use the sprint number as the row key
                  SprintNumber.Expression
                  //Use the state column as column key
                  (ColumnName.State.Expression)
                  //count the number of workitemids
                  Count WorkItemId.Expression
        ]  |> createTransformation "bugCountbySprint"

    let storyPointSumBySprint = 
        [
            only (SprintNumber.Expression |> isntMissing)
            only ((!> "StoryPoints") |> isntMissing)
            pivot 
                  //Use the sprint number as the row key
                  SprintNumber.Expression
                  //Use the state column as column key
                  //State.Expression 
                  (ColumnName.State.Expression)
                  //count the number of workitemids
                  Sum (!> "StoryPoints")
        ]   |> createTransformation "storyPointSumBySprint"    
    
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
        ]  |> createTransformation "simpleBurnUp"

    let burnUpWithForecast =
        [
            //index the rows by sprint number
            index rows by SprintNumber.Expression
            sort by SprintNumber.Name
            (* [ there's a bug making the calculator crash when runnning this transformation
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
            create SprintNumber.Name Keys *)
          ]  |> createTransformation "burnUpWithForecast"

    let workItemDoneMovingMean =
        [
            //remove all other columns than those metioned
            slice columns [SprintNumber.Name; "Done"]
            //moving mean and expanding sum only make sense if we are sure we know the order
            sort by SprintNumber.Name
            //Create a column named Velocity that's the moving mean of 'Done' of the last three rows
            create (column "Moving Mean") ((moving Mean 3 (!> "Done")))
        ]   |> createTransformation "workItemDoneMovingMean"

    let storyPointMovingMean =
        [
            //remove all other columns than those metioned
            slice columns [SprintNumber.Name; "Done"]
            //rename done to something better
            rename "Done" "StoryPoints"
            //moving mean and expanding sum only make sense if we are sure we know the order
            sort by SprintNumber.Name
            //Create a column named Velocity that's the moving mean of 'Done' of the last three rows
            create (column "Moving Mean") ((moving Mean 3 (!> "StoryPoints")))
        ]    |> createTransformation "storyPointMovingMean"

    let bugsPerSprint =
        [
            //remove all other columns than those metioned
            slice columns [SprintNumber.Name; "Done"]
            //rename done to something better
            rename "Done" "Bugs"
            //moving mean and expanding sum only make sense if we are sure we know the order
            sort by SprintNumber.Name
        ]   |> createTransformation "bugsPerSprint"       

    let martin =
        [
            slice columns [
                WorkItemId.Name
                "Area.AreaPath"
                "Iteration.IterationPath"
                State.Name
                "StoryPoints"
                "ClosedDate"
                "CreatedDate"
                WorkItemType.Name
                "Iteration.EndDate"
                "Iteration.StartDate"
                "TimeStamp"
            ]
        ]   |> createTransformation "martin"    
        
