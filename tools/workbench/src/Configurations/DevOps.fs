
namespace Workbench.Configurations
open Workbench.Types

module DevOps = 

  let projects = 
      [Project.Flowerpot]

module VCS =
    DevOps.projects 
    |> List.iter(fun p ->
        [
            Workbench.Transformations.Git.branchLifeTime
        ] |> addConfiguration (Source.Git(Branches,p)) "branchLifeTime" 
    )