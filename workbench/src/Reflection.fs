namespace Workbench

open Microsoft.FSharp.Quotations.Patterns
open Hobbes.Server.Db.DataConfiguration

type Project =
    Gandalf = 3
    | Momentum = 2
    | Flowerpot = 1
    | General = 0

type Source = 
    AzureDevOps = 1
    | Rally = 2
    | Jira = 3
    | Test = 4

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Project =
    let string (s: Project) =
        match s |> int with
        3 -> "gandalf"
        | 2 -> "momentum"
        | 1 -> "flowerpot"
        | 0 -> "general"
        | _ -> failwith "Can't happen"
        
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Source =
    let string (s: Source) =
        match s |> int with
        1 -> "azure devops"
        | 2 -> "rally"
        | 3 -> "jira"
        | 4 -> "test"
        | _ -> failwith "Can't happen"

type Configuration = 
    {
        Id : string
        Source : Source
        Project : Project
        Transformations : string list 
    }

type TransformationAttribute(order : int) =
    inherit System.Attribute()
    member __.Order with get() = order


type TransformationsAttribute(project : Project) =
    inherit System.Attribute()
    member __.Project with get() = project

type ConfigurationsAttribute(source : Source) =
    inherit System.Attribute()
    member __.Source with get() = source

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
        |> Seq.map(fun prop -> 
           prop.GetCustomAttributes(att,false) |> Array.head :?> 'att, (prop.DeclaringType.Name + "." + prop.Name, prop.GetValue(null) :?> 'a)
        )

    let private isTransformtion (f : System.Reflection.FieldInfo) =
        f.GetCustomAttributes(typeof<TransformationAttribute>,false) |> Seq.isEmpty |> not
        
    let transformations() =
        let collect = getPropertiesdWithAttribute<Transformation, TransformationAttribute>
        let types = getTypesWithAttribute<TransformationsAttribute>()
        types
        |> Seq.collect (collect)
        |> Seq.map snd
       
    let private createConfiguration (transformations : Map<_,_>) (configurationContainer : System.Type)  =
        configurationContainer
        |> getPropertiesdWithAttribute<Quotations.Expr<Transformation list>,ConfigurationAttribute>
        |> Seq.map(fun (att,(name, expr)) ->
            let rec readQuotation =
                function
                    PropertyGet(_,prop,_) -> 
                        [prop]
                    | NewUnionCase (_,exprs) -> //this is also the pattern for a list
                        exprs
                        |> List.collect(readQuotation)
                    | _ -> 
                        failwithf "Expected a transformation %A" expr
            let transformationsProperties = 
                readQuotation expr
            let trans = 
                transformationsProperties
                |> List.map(fun transformationProperty ->
                    transformationProperty.DeclaringType.Name + "." + transformationProperty.Name
                )
            {
                Id = (att.Project |> Project.string) + "." + name
                Source = 
                    (configurationContainer
                     |> tryGetAttribute<ConfigurationsAttribute> 
                     |> Option.get).Source
                Project = att.Project
                Transformations = transformations.[att.Project] @ trans
            }
        ) |> List.ofSeq
    let configurations() =
        let types = getTypesWithAttribute<ConfigurationsAttribute>() |> List.ofSeq
        let transformations = 
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
        |> Seq.map(createConfiguration transformations)|> Seq.collect id
       
        
        