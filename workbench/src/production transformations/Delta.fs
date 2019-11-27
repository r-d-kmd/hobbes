namespace Workbench.Transformations

[<Workbench.Transformations(Workbench.Project.Delta)>]
module Delta = 

    open Hobbes.DSL

    [<Workbench.Transformation 0 >]
    let renaming = 
        [
            only ( contains (!> "WorkItemType") [
                                                    !!> "User Story"
                                                    !!> "Bug"
                                                  ])
            rename "Iteration.IterationLevel3" "Sprint"
        ]