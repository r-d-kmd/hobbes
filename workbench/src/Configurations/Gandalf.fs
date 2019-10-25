[<Workbench.Configurations(Workbench.Source.AzureDevOps)>]
module State

[<Workbench.Configuration(Workbench.Project.Gandalf)>]
let stateBySprint =
    <@
        [
          Transformations.General.onlyUserStory
          Transformations.General.foldBySprint
          Transformations.Metrics.stateCountBySprint
        ]
    @>

[<Workbench.Configuration(Workbench.Project.Gandalf)>]
let expandingCompletionBySprint =
    <@
        [
          Transformations.General.onlyUserStory
          Transformations.General.foldBySprint
          Transformations.Metrics.stateCountBySprint
          Transformations.Metrics.expandingCompletionBySprint
        ]
    @>