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
  let userStoriesFoldedBySprint =
      <@
          [
            Transformations.General.foldBySprint
          ]
      @>  
      
  [<Configuration(Projects)>]
  let stateBySprint =
      <@
          [
            Transformations.General.foldBySprint
            Transformations.Metrics.stateCountBySprint
          ]
      @>

  [<Configuration(Projects)>]
  let expandingCompletionBySprint =
      <@
          [
            Transformations.General.foldBySprint
            Transformations.Metrics.stateCountBySprint
            Transformations.Metrics.expandingCompletionBySprint
          ]
      @>

  [<Configuration(Projects)>]
  let sprintVelocity =
      <@
          [
            Transformations.General.foldBySprint
            Transformations.Metrics.stateCountBySprint
            Transformations.Metrics.sprintVelocity
          ]
      @>