namespace Workbench.Configurations
open Workbench

[<Configurations(Source.AzureDevOps)>]
module State = 
  [<Literal>]
  let private Projects = 
      Project.Flowerpot 
      ||| Project.Gandalf 
      ||| Project.Delta 
      ||| Project.Momentum
      ||| Project.AzureDevOps
  
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
  let velocityMovingMean = 
      <@
          [
              Transformations.Momentum.velocityMean
          ]
      @>
