namespace Hobbes.Calculator.Services

open Hobbes.Web.Routing
open Hobbes.Web
open Hobbes.Web.Http
open FSharp.Data
open Hobbes.Web.RawdataTypes

[<RouteArea ("/data", false)>]
module Data =
    
    let configurations = Database.Database("configurations", Config.Parse, Log.loggerInstance)
    let transformations = Database.Database("transformations", Hobbes.Helpers.Json.deserialize<Transformation>, Log.loggerInstance)
   
    let inline private listConfigurations () = 
        configurations.List()
        |> Seq.filter(fun config ->
            config.JsonValue.Properties() 
            |> Array.tryFind(fun (name,_) -> name = "source") 
            |> Option.isSome
        )

    [<Get ("/collectors")>]
    let collectors () =
        200,(",",listConfigurations()
                  |> Seq.filter(fun config -> config.Source |> Option.isSome) 
                  |> Seq.map(fun config ->
                    config.Source.Value.Provider 
                  ) |> Seq.distinct
                  |> Seq.filter(System.String.IsNullOrWhiteSpace >> not)
                  |> Seq.map (sprintf "%A")
            ) |> System.String.Join
            |> sprintf "[%s]"

    [<Get ("/sources/%s")>]
    let sources (systemName:string) =
        200,(",\n",listConfigurations()
                  |> Seq.filter(fun config ->
                        match config.Source with
                        Some source -> source.Provider = systemName
                        | None -> false
                  ) |> Seq.map(fun config ->
                     config.Source.Value.JsonValue.ToString()
                  ) |> Seq.distinct
            ) |> System.String.Join
            |> sprintf "[%s]"


    [<Get ("/configuration/%s")>]
    let configuration (configurationName : string) =
        match configurations.TryGet configurationName with
        None -> 404, sprintf "Configuration (%s) not found" configurationName
        | Some c -> 200, c.JsonValue.ToString()

    
    [<Get ("/transformation/%s")>]
    let transformation (transformationName : string) =
        match transformations.TryGet transformationName with
        None -> 404, sprintf "Transformation (%s) not found" transformationName
        | Some transformation -> 200, transformation |> Hobbes.Helpers.Json.serialize

    [<Post ("/configuration", true)>]
    let storeConfiguration (configuration : string) =
        let conf = Config.Parse configuration
#if DEBUG
        let datasets = conf.Datasets
        let transformations = conf.Transformations
        Log.logf "Configuration %s. Transformations: %A Datasets: %A" configuration transformations datasets
        assert(System.String.IsNullOrWhiteSpace(conf.Id) |> not)
        assert(conf.Source |> Option.isNone || System.String.IsNullOrWhiteSpace(conf.Source.Value.Provider) |> not)
        assert(datasets.Length + transformations.Length > 0)
#endif
        200,configurations.InsertOrUpdate configuration

    [<Post ("/transformation", true)>]
    let storeTransformation (transformation : string) =
        let trans = 
            try
                Hobbes.Helpers.Json.deserialize<Transformation> transformation
            with e ->
               Log.excf e "Failed to deserialize %s" transformation
               reraise()
        eprintfn "Transformation: %s. #Statements: %d" transformation (trans.Statements.Length)
        assert(System.String.IsNullOrWhiteSpace(trans.Name) |> not)
        assert(trans.Statements |> List.isEmpty |> not)

        200,transformations.InsertOrUpdate transformation

    [<Get "/ping">]
    let ping () =
        200, "pong - Configurations"