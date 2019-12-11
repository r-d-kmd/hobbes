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

            create SprintNumber.Name Keys
            sort by SprintNumber.Name
        ]
    
    [<Workbench.Transformation 1>]
    let expandingCompletionBySprint =
        [
            slice columns [SprintNumber.Name; "Done"]
            sort by SprintNumber.Name
            create (column "Burn up") (expanding Sum (!> "Done")) 
            create (column "Velocity") ((moving Mean 3 (!> "Done")))
            index rows by SprintNumber.Expression
            create SprintNumber.Name Keys
            sort by SprintNumber.Name
            create (column "Burn up Prediction") ((linear extrapolationLimited) (!> "Burn up") 10 10)
        ]
        
    [<Workbench.Transformation 1>]
    let sprintVelocity =
        [
            sort by SprintNumber.Name
            index rows by (int SprintNumber.Expression)
            create (column "Velocity")  (moving Mean 3 (!> "Done"))
            create (column SprintNumber.Name) Keys
            slice columns [SprintNumber.Name; "Velocity"]
        ]