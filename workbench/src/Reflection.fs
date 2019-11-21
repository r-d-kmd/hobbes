namespace Workbench

open Hobbes.Server.Reflection

type Source = 
    AzureDevOps = 1
    | Rally = 2
    | Jira = 3
    | Test = 4

[<System.FlagsAttribute>]
type Project =
    Delta = 256
    | EzEnergy = 128
    | Gandalf = 64
    | Momentum = 32
    | Flowerpot = 16
    | Jira = 8
    | AzureDevOps = 4
    | Rally = 2
    | General = 1



[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Project =
    let string (s: Project) =
        match s with
        Project.Gandalf -> "gandalf"
        | Project.Momentum -> "momentum"
        | Project.Flowerpot -> "flowerpot"
        | Project.AzureDevOps -> "azuredevops"
        | _ -> failwith "Can't happen"

    let sourceProject (p : Project)=
       match p with
       Project.Gandalf 
       | Project.Momentum | Project.Flowerpot | Project.AzureDevOps -> Project.AzureDevOps
       | _ -> failwith "Shouldn't happen"

    let toList (p: Project) =
        let rec inner (n : int) acc = 
            let acc = 
                if (p |> int) &&& n = n then (enum<Project> n)::acc
                else acc
            if n = 1 then acc
            else inner (n/2) acc
        let maxValue = 
            [ for i in System.Enum.GetValues(typeof<Project>) ->  i |> unbox |> int] |> List.max
        inner maxValue [] 
        
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Source =
    let string (s: Source) =
        match s with
        Source.AzureDevOps -> "azure devops"
        | Source.Rally -> "rally"
        | Source.Jira -> "jira"
        | Source.Test -> "test"
        | _ -> failwith "Can't happen"

type Configuration = 
    {
        Id : string
        Source : Source
        Project : Project
        Transformations : string list 
    }

[<System.AttributeUsage(System.AttributeTargets.Property, 
                        Inherited = false, 
                        AllowMultiple = false)>]
type TransformationAttribute(order : int) =
    inherit System.Attribute()
    member __.Order with get() = order


[<System.AttributeUsage(System.AttributeTargets.Class, 
                        Inherited = false)>]
type TransformationsAttribute(project : Project ) =
    inherit System.Attribute()
    member __.Project with get() = project

[<System.AttributeUsage(System.AttributeTargets.Class, 
                        Inherited = false)>]
type ConfigurationsAttribute(source : Source) =
    inherit System.Attribute()
    member __.Source with get() = source

[<System.AttributeUsage(System.AttributeTargets.Property, 
                         Inherited = false, 
                         AllowMultiple = false)>]
type ConfigurationAttribute(project : Project) =
    inherit System.Attribute()
    member __.Project with get() = project
 
type Transformation = Hobbes.DSL.Statements list
module Reflection =
    
    let private isTransformtion (f : System.Reflection.FieldInfo) =
        f.GetCustomAttributes(typeof<TransformationAttribute>,false) |> Seq.isEmpty |> not
        
    let transformations() =
        let collect = getPropertiesdWithAttribute<Transformation, TransformationAttribute>
        let types = 
            getTypesWithAttribute<TransformationsAttribute>()
            |> Seq.toList
        types
        |> Seq.collect (collect)
        |> Seq.map snd

    let private createConfiguration (projectTransformations : Map<_,_>) (configurationContainer : System.Type)  =
        configurationContainer
        |> getPropertiesdWithAttribute<Quotations.Expr<Transformation list>,ConfigurationAttribute>
        |> Seq.collect(fun (att,(name, expr)) ->
            let projects = 
                att.Project |> Project.toList

            let transformationsProperties = 
                readQuotation expr
                |> List.filter(fun t -> t.GetType() = typeof<System.Reflection.PropertyInfo>)

            let trans = 
                (transformationsProperties)
                |> List.map(fun transformationProperty ->
                    transformationProperty.DeclaringType.Name + "." + transformationProperty.Name
                )

            projects
            |> Seq.map(fun project ->
                let projectTrans =
                    match projectTransformations |> Map.tryFind project with
                    None -> []
                    | Some t -> t 

                let sourceTrans =
                    match projectTransformations |> Map.tryFind (project |> Project.sourceProject) with
                    None -> []
                    | Some t -> t
                {
                    Id = (project |> Project.string) + "." + name
                    Source = 
                        (configurationContainer
                         |> tryGetAttribute<ConfigurationsAttribute> 
                         |> Option.get).Source
                    Project = project
                    Transformations = 
                       projectTrans@sourceTrans@trans
                }
            )
        ) |> List.ofSeq
    let configurations() =
        let types = getTypesWithAttribute<ConfigurationsAttribute>() |> List.ofSeq
        let projectTransformations = 
            getTypesWithAttribute<TransformationsAttribute>()
            |> Seq.map(fun t -> 
                (t 
                 |> (tryGetAttribute<TransformationsAttribute> >> Option.get)).Project, 
                t 
                |> getPropertiesdWithAttribute<Transformation, TransformationAttribute> 
                |> Seq.sortBy(fun (att,_) -> att.Order) 
                |> Seq.map(fun (_,(n,_)) -> n)
                |> List.ofSeq
            )
            |> Map.ofSeq
        types
        |> Seq.map(createConfiguration projectTransformations)|> Seq.collect id 
        