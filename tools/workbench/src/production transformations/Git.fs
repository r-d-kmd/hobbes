namespace Workbench.Transformations

[<Workbench.Transformations(Workbench.Project.Flowerpot ||| Workbench.Project.Branches)>]
module Git =

    open Hobbes.Parsing.AST
    open Hobbes.DSL

    [<Workbench.Transformation 1>]
    let branchLifeTime =
        [
            create "Branch Life Time" (
                expanding Mean (
                    !> "BranchLifeTimeInHours"
                )
            )                                
        ]