namespace Workbench.Transformations
open Hobbes.Parsing.AST

[<Workbench.Transformations(Workbench.Project.General)>]
module Metrics = 

    open Hobbes.DSL
    open General

    [<Workbench.Transformation 0>]
    let stateCountBySprint = 
        [
            pivot 
                  SprintNumber.Expression 
                  State.Expression 
                  Count 
                  WorkItemId.Expression
        ]
    
    [<Workbench.Transformation 1>]
    let expandingCompletionBySprint =
        [
            slice columns [SprintNumber.Name; "Done"]
            index rows by SprintNumber.Expression
            sort by SprintNumber.Name
            create (column "Burn up") (expanding Sum (!> "Done")) 
            create (column "Velocity") ((moving Mean 3 (!> "Done")))
            create (column "Burn up Prediction") ((linear extrapolationLimited) (!> "Burn up") 10 10)
            slice columns [
                            SprintNumber.Name
                            "Done"
                            "Velocity"
                            "Burn up"
                            "Burn up Prediction"
                          ]
        ]
        
    [<Workbench.Transformation 1>]
    let sprintVelocity =
        [
            index rows by (int SprintNumber.Expression)
            create (column "Velocity")  (moving Mean 3 (!> "Done"))
            slice columns [SprintNumber.Name; "Velocity"]
        ]