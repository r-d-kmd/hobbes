namespace Workbench

open Microsoft.FSharp.Quotations.Patterns
type Source = 
    AzureDevOps = 1
    | Rally = 2
    | Jira = 3
    | Test = 4

type Project =
    AzureDevOps = 4
    | Gandalf = 3
    | Momentum = 2
    | Flowerpot = 1
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
    let tryGetAttribute<'a> (m:System.Reflection.MemberInfo) : 'a option= 
        match m.GetCustomAttributes(typeof<'a>,false) with
        [||] -> None
        | a -> a |> Array.head :?> 'a |> Some

    let hasAttribute t (m:#System.Reflection.MemberInfo) =
        m.GetCustomAttributes(t,false) |> Seq.isEmpty |> not
     
    let private filterByAttribute attributeType (types : seq<#System.Reflection.MemberInfo>) =
        types |> Seq.filter(hasAttribute attributeType)

    let private  getTypesWithAttribute<'a>() =
        let att = typeof<'a> 
        System.Reflection.Assembly.GetExecutingAssembly().GetTypes()
        |> filterByAttribute att

    let private getPropertiesdWithAttribute<'a,'att> (t: System.Type) =
        let flags = System.Reflection.BindingFlags.Static ||| System.Reflection.BindingFlags.Public
        let att = typeof<'att>
        let props = t.GetProperties(flags)
        props |> filterByAttribute att
        |> Seq.collect(fun prop -> 
           prop.GetCustomAttributes(att,false)
           |> Array.map(fun a -> 
               a :?> 'att, (prop.DeclaringType.Name + "." + prop.Name, prop.GetValue(null) :?> 'a)
           )
        )

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
            let rec readQuotation =
                function
                    PropertyGet(_,prop,_) -> 
                        [prop]
                    | NewUnionCase (_,exprs) ->
                        //this is also the pattern for a list
                        let elements =  
                            exprs
                            |> List.fold(fun res expr ->
                                res@(readQuotation expr)
                            ) [] 
                        elements
                    | _ -> 
                        failwithf "Expected a transformation %A" expr
            let transformationsProperties = 
                readQuotation expr
            let trans = 
                transformationsProperties
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
       
        
        