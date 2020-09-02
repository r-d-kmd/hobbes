namespace Workbench.Configurations
open Workbench.Types

module DevOps = 

    let projects = 
       [
          Project.Flowerpot 
          Project.Gandalf 
          Project.Delta 
          Project.Momentum
          Project.Nexus
          Project.UVskole
          Project.Logic
       ]

    let initialise() = 
        projects 
        |> List.iter(fun p ->
            [
                Workbench.Transformations.Git.allCommits
            ] |> addConfiguration Production (Source.Git(Commits,p)) "allCommits"
        )