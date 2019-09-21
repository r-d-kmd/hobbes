module DataConfiguration
open FSharp.Data

let get configurationName =
    configurationName
    |> Database.configurations.Get     
    