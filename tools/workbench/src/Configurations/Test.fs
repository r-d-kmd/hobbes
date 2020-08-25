namespace Workbench.Configurations
open Workbench.Types
open Workbench.Transformations

module Test = 

    let projects = 
      [
        Project.Flowerpot 
        Project.Gandalf
        Project.Nexus
      ]
    
    let addGitConfiguraiton = addConfiguration Test (Source.Git(Commits,project))
    let addAzureConfiguration conf = 
          projects
          |> List.map(fun project -> addConfiguration Test (Source.AzureDevOps(project)) conf)
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
            |> addAzureConfiguration name
            next
        ) [] |> ignore

        [
          Metrics.martin
        ] |> addAzureConfiguration "martin"
        [
          Metrics.martin
        ] |> addConfiguration Test (Source.AzureDevOps(Project.Gandalf)) "martin"