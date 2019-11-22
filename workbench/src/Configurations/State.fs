namespace Workbench.Configurations
open Workbench

[<Configurations(Source.AzureDevOps)>]
module State = 
  [<Literal>]
  let private Projects = Project.Flowerpot ||| Project.Gandalf ||| Project.Delta

  [<Configuration(Projects)>]
  let onlyUserStories =
      <@
          [
            Transformations.General.onlyUserStory
          ]
      @>
  
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