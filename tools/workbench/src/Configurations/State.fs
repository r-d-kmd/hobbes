namespace Workbench.Configurations
open Workbench
open Workbench.Transformations

module State = 

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
  let uniformingTransformations = 
      [
          Project.Flowerpot , [Flowerpot.renaming]
          Project.Gandalf, [Gandalf.renaming]
          Project.Delta, [Delta.renaming]
          Project.Momentum, [Momentum.renaming]
          Project.Nexus, [Nexus.renaming]
          Project.UVskole, [UVskole.renaming]
          Project.Logic, [KMDLoGIC.renaming]
      ] |> List.map(fun (p,lst) -> p,(Azure.stateRenaming::(lst |> List.rev)) |> List.rev )
      |> Map.ofList

  let add name transformations = 
      projects
      |> List.iter(fun project ->
        let source = 
            project |> Source.AzureDevOps
        let transformations = 
            uniformingTransformations.[project]@transformations
        Types.addConfiguration Production source name transformations
      )
  
  let initialise() = 
    [
      General.foldBySprint
    ] |> add "foldBySprint"
       
    [
      General.foldBySprint
      Metrics.stateCountBySprint
    ] |> add "stateCountBySprint"
    let simpleBurnUp = 
      [
        General.foldBySprint
        Metrics.stateCountBySprint
        Metrics.simpleBurnUp
      ]  
    simpleBurnUp |> add "simpleBurnUp"

    [
      Project.Flowerpot
      Project.Delta
    ] |> List.iter(fun p -> 
        (uniformingTransformations.[p])@simpleBurnUp |> Types.addConfiguration Test (Source.AzureDevOps p) "simpleBurnUp" 
    )

    [
      General.foldBySprint
      Metrics.stateCountBySprint
      Metrics.simpleBurnUp
      Metrics.burnUpWithForecast
    ]  |> add "burnUpWithForecast"

    [
      General.foldBySprint
      Metrics.stateCountBySprint
      Metrics.workItemDoneMovingMean
    ] |> add "workItemMovingMean"

    [
      General.foldBySprint
      Metrics.storyPointSumBySprint
      Metrics.storyPointMovingMean
    ] |> add "storyPOintsMovingMean"

    [
      General.foldBySprint
      Metrics.bugCountBySprint
      Metrics.bugsPerSprint
    ] |> add "bugsPerSprint"

    [
      Metrics.martin
    ]  |> add "martin"

