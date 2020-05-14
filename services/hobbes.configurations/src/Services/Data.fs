namespace Hobbes.Calculator.Services

open Hobbes.Web.Routing
open Hobbes.Web
open Hobbes.Web.Http
open FSharp.Data
open Hobbes.Shared.RawdataTypes

[<RouteArea ("/data", false)>]
module Data =
    
    let configurations = Database.Database("configurations", Config.Parse, Log.loggerInstance)
    let transformations = Database.Database("transformations", TransformationRecord.Parse, Log.loggerInstance)
   

    [<Get ("/sources/%s")>]
    let sources (systemName:string) =
        200,("\n",configurations.List()
                 |> Seq.filter(fun config ->
                     config.JsonValue.Properties() 
                     |> Array.tryFind(fun (name,_) -> name = "source") 
                     |> Option.isSome &&
                       config.Source.Name = systemName
                 ) |> Seq.map(fun config ->
                    config.Source.Name
                 ) |> Seq.distinct
            ) |> System.String.Join

    [<Get ("/configuration/%s")>]
    let configuration (configurationName : string) =
        match configurations.TryGet configurationName with
        None -> 404, sprintf "Configuration (%s) not found" configurationName
        | Some c -> 200, c.JsonValue.ToString()

    [<Get ("/transformation/%s")>]
    let transformation (transformationName : string) =
        match transformations.TryGet transformationName with
        None -> 404, sprintf "Transformation (%s) not found" transformationName
        | Some c -> 200, c.JsonValue.ToString()

    [<Post ("/configuration", true)>]
    let storeConfiguration (configuration : string) =
        let conf = Config.Parse configuration
        
        assert(System.String.IsNullOrWhiteSpace(conf.Id) |> not)
        assert(System.String.IsNullOrWhiteSpace(conf.Source.Name) |> not)
        assert(conf.Transformations |> Array.isEmpty |> not)

        200,configurations.InsertOrUpdate configuration

    [<Post ("/transformation", true)>]
    let storeTransformation (transformation : string) =
        let trans = TransformationRecord.Parse transformation

        assert(System.String.IsNullOrWhiteSpace(trans.Id) |> not)
        assert(trans.Lines |> Array.isEmpty |> not)

        200,transformations.InsertOrUpdate transformation

    [<Get "/ping">]
    let ping () =
        200, "ping - Configurations"