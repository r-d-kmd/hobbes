namespace Workbench

open Hobbes.Server.Reflection

type Source = 
    AzureDevOps = 1
    | Rally = 2
    | Jira = 3
    | Test = 4

[<System.FlagsAttribute>]
type Project =
    UVskole = 1024
    | Nexus = 512
    | Delta = 256
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
    let source (p : Project)=
        match (p &&& Project.AzureDevOps)
               ||| (p &&& Project.Rally)
               ||| (p &&& Project.Jira) with
        Project.AzureDevOps 
        | Project.Rally 
        | Project.Jira  as p -> p
        | _ -> 
            eprintfn "No source specified %A" p
            Project.General

    let toList (p: Project) =
        let rec inner (n : int) acc = 
            if n = int Project.Jira then acc
            else
                let acc = 
                    if (p |> int) &&& n = n then (enum<Project> n)::acc
                    else acc
                
                inner (n/2) acc

        let maxValue = 
            [ for i in System.Enum.GetValues(typeof<Project>) ->  i |> unbox |> int] |> List.max

        inner maxValue [] 
    let name (p: Project) =
        match p |> toList with
        Project.Gandalf::_ -> "gandalf"
        | Project.Momentum::_ -> "momentum"
        | Project.Flowerpot::_ -> "flowerpot"
        | Project.AzureDevOps::_ -> "azuredevops"
        | Project.Delta::_ -> "delta"
        | Project.Nexus::_ -> "nexus"
        | Project.UVskole::_ -> "uvskole"
        | _ -> failwith "Can happen"

    let configString (s: Project) =
        let p = s |> toList |> List.head //removes any source 'projects'
        let projectName = p |> name

        match s |> source with
        Project.AzureDevOps ->
            let detailedSourceConfig account = 
                sprintf """ "azureDevOps" : {
                                "account" : "%s",
                                "project" : "%s"
                            }""" account projectName
            match p with
            Project.Delta ->
                detailedSourceConfig "time-payroll-kmddk"
            | _ -> 
                detailedSourceConfig "kmddk"
        | _ -> failwith "Project source not supported yet!"
        
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Source =
    let project (s: Source) =
        match s with
        Source.AzureDevOps -> Project.AzureDevOps
        | Source.Rally -> Project.Rally
        | Source.Jira -> Project.Jira
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
        let modules = 
            getModulesWithAttribute<TransformationsAttribute>(System.Reflection.Assembly.GetExecutingAssembly())
            |> Seq.toList
        modules
        |> Seq.collect (collect)
        |> Seq.map snd

    let private createConfiguration (projectTransformations : Map<_,_>) (configurationContainer : System.Type)  =
        configurationContainer
        |> getPropertiesdWithAttribute<Quotations.Expr<Transformation list>,ConfigurationAttribute>
        |> Seq.collect(fun (att,(name, expr)) ->
            let source = att.Project |> Project.source
            let projects = 
                att.Project 
                |> Project.toList
                |> List.map(fun p -> p ||| source )
            
            
            let transformationsProperties = 
                readQuotation expr

            let trans = 
                transformationsProperties
                |> List.map(fun transformationProperty ->
                    transformationProperty.DeclaringType.Name + "." + transformationProperty.Name
                )
            
            projects
            |> Seq.map(fun project ->
                let p = project |> Project.toList |> List.head
                let projectTrans =
                    match projectTransformations |> Map.tryFind p with
                    None -> []
                    | Some t -> t 

                let sourceTrans =
                    match projectTransformations |> Map.tryFind (project |> Project.source) with
                    None -> []
                    | Some t -> t
                {
                    Id = (project |> Project.name) + "." + name
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
        let asm = System.Reflection.Assembly.GetExecutingAssembly()
        let modules = getModulesWithAttribute<ConfigurationsAttribute>(asm) |> List.ofSeq
        let projectTransformations = 
            getModulesWithAttribute<TransformationsAttribute> asm
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
        modules
        |> Seq.map(createConfiguration projectTransformations)|> Seq.collect id 
        