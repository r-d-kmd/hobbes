namespace Workbench.Transformations
open Workbench.Types

module Git =

    open Hobbes.Parsing.AST
    open Hobbes.DSL

    let branchLifeTime =
        [
            create "Branch Life Time" (
                expanding Mean (
                    !> "BranchLifeTimeInHours"
                )
            )                                
        ] |> Transformation.Create "branchLifeTime"