namespace Workbench.Configurations
open Workbench.Types
open Workbench.Transformations

module MergeJoinTest = 

    let initialise() = 
            [General.all] |> addConfiguration Test (Source.Merge  ["azureDevops.Flowerpot.martin"; "azureDevops.Flowerpot.martin"]) "MergeTest"