namespace Workbench

open Hobbes.Server.Reflection

type Source = 
    AzureDevOps = 1
    | Rally = 2
    | Jira = 3
    | Test = 4

type Project =
    EzEnergy = 7
    | Gandalf = 6
    | Momentum = 5
    | Flowerpot = 4
    | Jira = 3
    | AzureDevOps = 2
    | Rally = 1
    | General = 0


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
                        AllowMultiple = true)>]
type TransformationAttribute(order : int) =
    inherit System.Attribute()
    member __.Order with get() = order


[<System.AttributeUsage(System.AttributeTargets.Class, 
                        Inherited = false)>]
type TransformationsAttribute(project : Project) =
    inherit System.Attribute()
    member __.Project with get() = project

[<System.AttributeUsage(System.AttributeTargets.Class, 
                        Inherited = false)>]
type ConfigurationsAttribute(source : Source) =
    inherit System.Attribute()
    member __.Source with get() = source

[<System.AttributeUsage(System.AttributeTargets.Property, 
                         Inherited = false, 
                         AllowMultiple = true)>]
type ConfigurationAttribute(project : Project) =
    inherit System.Attribute()
    member __.Project with get() = project
 
type Transformation = Hobbes.DSL.Statements list
module Reflection =
    
    let private isTransformtion (f : System.Reflection.FieldInfo) =
        f.GetCustomAttributes(typeof<TransformationAttribute>,false) |> Seq.isEmpty |> not
        
    let transformations() =
        let collect = getPropertiesdWithAttribute<Transformation, TransformationAttribute>
        let types = getTypesWithAttribute<TransformationsAttribute>()
        types
        |> Seq.collect (collect)
        |> Seq.map snd

    let private createConfiguration (projectTransformations : Map<_,_>) (configurationContainer : System.Type)  =
        configurationContainer
        |> getPropertiesdWithAttribute<Quotations.Expr<Transformation list>,ConfigurationAttribute>
        |> Seq.map(fun (att,(name, expr)) ->
            
            let transformationsProperties = 
                readQuotation expr
                |> List.filter(fun t -> t.GetType() = typeof<System.Reflection.PropertyInfo>)
            let trans = 
                (transformationsProperties)
                |> List.map(fun transformationProperty ->
                    transformationProperty.DeclaringType.Name + "." + transformationProperty.Name
                )
            let projectTrans =
                match projectTransformations |> Map.tryFind att.Project with
                None -> []
                | Some t -> t 

            let sourceTrans =
                match projectTransformations |> Map.tryFind (att.Project |> Project.sourceProject) with
                None -> []
                | Some t -> t
            {
                Id = (att.Project |> Project.string) + "." + name
                Source = 
                    (configurationContainer
                     |> tryGetAttribute<ConfigurationsAttribute> 
                     |> Option.get).Source
                Project = att.Project
                Transformations = 
                   projectTrans@sourceTrans@trans
            }
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
       
        
        