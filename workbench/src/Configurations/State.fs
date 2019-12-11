namespace Workbench.Configurations
open Workbench

[<Configurations(Source.AzureDevOps)>]
module State = 
  [<Literal>]
  let private Projects = 
      Project.Flowerpot 
      ||| Project.Gandalf 
      ||| Project.Delta 
      ||| Project.AzureDevOps

  [<Configuration(Projects)>]
  let onlyUserStories =
      <@
          [
            Transformations.General.onlyUserStory
          ]
      @>
  
  [<Configuration(Projects)>]
  let baseInformations : Quotations.Expr<Hobbes.DSL.Statements list list> =
      <@ [] @>

  [<Configuration(Projects)>]
  let userStoriesFoldedBySprint =
      <@
          [
            Transformations.General.onlyUserStory
            Transformations.General.foldBySprint
          ]
      @>  
      
  [<Configuration(Projects)>]
  let stateBySprint =
      <@
          [
            Transformations.General.onlyUserStory
            Transformations.General.foldBySprint
            Transformations.Metrics.stateCountBySprint
          ]
      @>

  [<Configuration(Projects)>]
  let expandingCompletionBySprint =
      <@
          [
            Transformations.General.onlyUserStory
            Transformations.General.foldBySprint
            Transformations.Metrics.stateCountBySprint
            Transformations.Metrics.expandingCompletionBySprint
          ]
      @>

  [<Configuration(Projects)>]
  let sprintVelocity =
      <@
          [
            Transformations.General.onlyUserStory
            Transformations.General.foldBySprint
            Transformations.Metrics.stateCountBySprint
            Transformations.Metrics.sprintVelocity
          ]
      @>