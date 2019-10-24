[<Workbench.Configurations(Workbench.Source.AzureDevOps)>]
module State

[<Workbench.Configuration(Workbench.Project.Gandalf)>]
let stateBySprint =
    <@
        Transformations.General.foldBySprint
    @>