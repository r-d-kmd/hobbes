namespace Hobbes.Calculator.Services

open Hobbes.Web.Routing
open Hobbes.Web
open Hobbes.Web.Http
open FSharp.Data
open Hobbes.Web.RawdataTypes

[<RouteArea ("/data", false)>]
module Data =
    
    let configurations = Database.Database("configurations", Config.Parse, Log.loggerInstance)
    let transformations = Database.Database("transformations", TransformationRecord.Parse, Log.loggerInstance)
   
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
                  |> Seq.map(fun config ->
                    config.Source.Name 
                  ) |> Seq.distinct
                  |> Seq.filter(fun s -> System.String.IsNullOrWhiteSpace s |> not)
                  |> Seq.map (sprintf "%A")
            ) |> System.String.Join
            |> sprintf "[%s]"

    [<Get ("/sources/%s")>]
    let sources (systemName:string) =
        200,(",\n",listConfigurations()
                  |> Seq.filter(fun config ->
                        config.Source.Name = systemName
                  ) |> Seq.map(fun config ->
                     config.Source.JsonValue.ToString()
                  ) |> Seq.distinct
            ) |> System.String.Join
            |> sprintf "[%s]"


    [<Get ("/configuration/%s")>]
    let configuration (configurationName : string) =
        match configurations.TryGet configurationName with
        None -> 404, sprintf "Configuration (%s) not found" configurationName
        | Some c -> 200, c.JsonValue.ToString()

    [<Get ("/dependingtransformations/%s")>]
    let dependingTransformations (cacheKey : string) =
        if cacheKey.EndsWith(":") then 404, "Invalid cache key"
        else
            let dependencies = 
                configurations.List()
                |> Seq.collect(fun configuration ->
                    let transformations = 
                        configuration.Transformations
                        |> Array.map(fun transformationName ->
                            match transformations.TryGet transformationName with
                            None -> 
                                Log.errorf  "Transformation (%s) not found" transformationName
                                None
                            | t -> t
                        ) |> Array.filter Option.isSome
                        |> Array.map Option.get
                        |> Array.toList

                    match transformations with
                    [] -> []
                    | h::tail ->
                        tail
                        |> List.fold(fun (lst : (string * TransformationRecord.Root) list) t ->
                            let prevKey, prevT = lst |> List.head
                            (prevKey + ":" + prevT.Id,t) :: lst
                        ) [keyFromConfig configuration,h]
                ) |> Seq.groupBy fst
                |> Seq.map(fun (key,deps) ->
                    key,
                        deps 
                        |> Seq.map snd 
                        |> Seq.distinctBy(fun t -> t.Id)
                ) |> Map.ofSeq
            match dependencies |> Map.tryFind cacheKey with
            None -> 404,sprintf "No dependencies found for key (%s)" cacheKey
            | Some dependencies ->
                   200,System.String.Join(",",dependencies)
                       |> sprintf "[%s]"
            
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
        200, "pong - Configurations"