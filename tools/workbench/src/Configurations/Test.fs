namespace Workbench.Configurations
open Workbench.Types
open Workbench.Transformations

module Test = 

    let projects = 
      [
        Project.Flowerpot 
        //Project.Gandalf
        //Project.Nexus
      ]
    
    let addGitConfiguraiton name transformations = 
          projects
          |> List.iter(fun project -> addConfiguration Test (Source.Git(Commits,project)) name transformations)
    let addAzureConfiguration name transformations = 
          projects
          |> List.iter(fun project -> addConfiguration Test (Source.AzureDevOps(project)) name transformations)
    let initialise() = 
        [
            Git.allCommits
        ] |> addGitConfiguraiton "allCommits"

        [
            Flowerpot.renaming,"renamed"
            Azure.stateRenaming,"stateRenaming"
            General.foldBySprint,"foldBySprint"
            General.onlyInSprint,"onlyInSprint"
        ] |> List.fold(fun previous (current,name) ->
            let next = current::previous
            next
            |> List.rev
            |> addConfiguration Test (Source.AzureDevOps(Project.Flowerpot)) name
            next
        ) [] |> ignore

        [
          Metrics.martin
        ] |> addAzureConfiguration "martin"