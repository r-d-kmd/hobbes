
namespace Workbench.Configurations
open Workbench.Types

module DevOps = 

    let projects = 
      [
          Project.Flowerpot
          Project.Delta
      ]

    let initialise() = 
        projects 
        |> List.iter(fun p ->
            [
                Workbench.Transformations.Git.branchLifeTime
            ] |> addConfiguration (Source.Git(Branches,p)) "branchLifeTime" 
        )