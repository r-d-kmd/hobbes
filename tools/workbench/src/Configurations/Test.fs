namespace Workbench.Configurations
open Workbench.Types
open Workbench.Transformations

module Test = 
    let testConfig() = 
        [
          Flowerpot.renaming
        ] |> addConfiguration Test (Source.Local("test",
                                                 [
                                                   "WorkItemId"
                                                   "WorkItemType"
                                                   "Iteration.IterationLevel2"
                                                 ],
                                                 [ for i in 88108..88115 ->
                                                   [
                                                     i
                                                     (match i % 3 with
                                                      0 -> "User Story"
                                                      | 1 -> "Bug"
                                                      | _ -> "Task"
                                                     )
                                                     (sprintf "Iteration %d" (i % 10))
                                                    ]
                                                 ])) "localtest"
    let initialise() = 

        [
            Flowerpot.renaming,"renamed"
            Azure.renaming,"stateRenaming"
            Azure.uniformWorkItems,"uniformWorkItems"
        ]  |> List.map (fst)
        |> addConfiguration Test (Source.AzureDevOps(Project.Flowerpot)) "Test"

        