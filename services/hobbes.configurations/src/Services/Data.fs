namespace Hobbes.Calculator.Services

open Hobbes.Server.Routing
open Hobbes.Web
open Hobbes.Web.Http
open FSharp.Data
open Hobbes.Shared.RawdataTypes

[<RouteArea ("/data", false)>]
module Data =
    
    let configurations = Database.Database("configurations", Config.Parse, Log.loggerInstance)
    let transformations = Database.Database("transformations", TransformationRecord.Parse, Log.loggerInstance)
   
    [<Get ("/configuration/%s")>]
    let configuration (configurationName : string) =
        match configurations.TryGet configurationName with
        None -> 404, "Configuration not found"
        | Some c -> 200, c.JsonValue.ToString()

    [<Get ("/transformation/%s")>]
    let transformation (transformationName : string) =
        match transformations.TryGet transformationName with
        None -> 404, "Transformation not found"
        | Some c -> 200, c.JsonValue.ToString()

    [<Post ("/configuration", true)>]
    let storeConfiguration (configuration : string) =
        200,configurations.InsertOrUpdate configuration

    [<Post ("/transformation", true)>]
    let storeTransformation (transformation : string) =
        200,transformations.InsertOrUpdate transformation

    [<Get "/ping">]
    let ping () =
        200, "ping - Configurations"