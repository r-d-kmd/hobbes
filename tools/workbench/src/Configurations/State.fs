namespace Workbench.Configurations
open Workbench

[<Configurations(Source.AzureDevOps)>]
module State = 

  [<Literal>]
  let Projects = 
      Project.AzureDevOps
      ||| Project.Flowerpot 
      ||| Project.Gandalf 
      ||| Project.Delta 
      ||| Project.Momentum
      ||| Project.Nexus
      ||| Project.UVskole
  
  [<Configuration(Projects)>]
  let baseInformations : Quotations.Expr<Hobbes.DSL.Statements list list> =
      <@ [] @>

  [<Configuration(Projects)>]
  let foldBySprint =
      <@
          [
            Transformations.General.foldBySprint
          ]
      @>  
      
  [<Configuration(Projects)>]
  let stateCountBySprint =
      <@
          [
            Transformations.General.foldBySprint
            Transformations.Metrics.stateCountBySprint
          ]
      @>

  [<Configuration(Projects)>]
  let simpleBurnUp =
      <@
          [
            Transformations.General.foldBySprint
            Transformations.Metrics.stateCountBySprint
            Transformations.Metrics.simpleBurnUp
          ]

      @>
  [<Configuration(Projects)>]
  let burnUpWithForecast =
      <@
          [
            Transformations.General.foldBySprint
            Transformations.Metrics.stateCountBySprint
            Transformations.Metrics.simpleBurnUp
            Transformations.Metrics.burnUpWithForecast
          ]
      @>

  [<Configuration(Projects)>]
  let workItemMovingMean = 
      <@
          [
            Transformations.General.foldBySprint
            Transformations.Metrics.stateCountBySprint
            Transformations.Metrics.workItemDoneMovingMean
          ]
      @>

  [<Configuration(Projects)>]
  let storyPointsMovingMean = 
      <@
          [
            Transformations.General.foldBySprint
            Transformations.Metrics.storyPointSumBySprint
            Transformations.Metrics.storyPointMovingMean
          ]
      @>

  [<Configuration(Projects)>]
  let bugsPerSprint = 
      <@
          [
            Transformations.General.foldBySprint
            Transformations.Metrics.bugCountBySprint
            Transformations.Metrics.bugsPerSprint
          ]
      @>

  [<Configuration(Projects)>]
  let martin = 
      <@
          [
            Transformations.Metrics.martin
          ]
      @>

[<Configurations(Source.GitBranches)>]
module VCS =
    [<Configuration(State.Projects)>]
    let branchLifeTime =
        <@
            [
                Transformations.Git.branchLifeTime
            ]
        @>