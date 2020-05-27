
namespace Workbench.Configurations
open Workbench

[<Configurations(Source.Branches)>]
module DevOps = 

  [<Literal>]
  let Projects = 
      Project.Flowerpot 
      ||| Project.Gandalf 
      ||| Project.Delta 
      ||| Project.Momentum
      ||| Project.Nexus
      ||| Project.UVskole
      ||| Project.Branches

[<Configurations(Source.Branches)>]
module VCS =
    [<Configuration(DevOps.Projects)>]
    let branchLifeTime =
        <@
            [
                Transformations.Git.branchLifeTime
            ]
        @>