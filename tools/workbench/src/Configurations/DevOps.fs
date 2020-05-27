
namespace Workbench.Configurations
open Workbench

[<Configurations(Source.Branches)>]
module DevOps = 

  [<Literal>]
  let Projects = 
      Project.Flowerpot

[<Configurations(Source.Branches)>]
module VCS =
    [<Configuration(DevOps.Projects)>]
    let branchLifeTime =
        <@
            [
                Transformations.Git.branchLifeTime
            ]
        @>