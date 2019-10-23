namespace Workbench

type TransformationAttribute() =
    inherit System.Attribute()


type TransformationsAttribute() =
    inherit System.Attribute()

type ConfigurationAttribute() =
    inherit System.Attribute()

module Reflection =
    let hasAttribute t (m:#System.Reflection.MemberInfo) =
        m.GetCustomAttributes(t,false) |> Seq.isEmpty |> not

    let private filterByAttribute attributeType (types : seq<#System.Reflection.MemberInfo>) =
        types |> Seq.filter(hasAttribute attributeType)

    let private  getTypes() = 
        System.Reflection.Assembly.GetExecutingAssembly().GetTypes()
        |> filterByAttribute typeof<TransformationsAttribute>

    let private getTransformations (t: System.Type) =
        t.GetFields()
        |> filterByAttribute typeof<TransformationAttribute>

    let private isTransformtion (f : System.Reflection.FieldInfo) =
        f.GetCustomAttributes(typeof<TransformationAttribute>,false) |> Seq.isEmpty |> not
        
    let transformations() =
       getTypes()
       |> Seq.collect getTransformations
       |> Seq.map(fun field -> 
           field.DeclaringType.Name + "." + field.Name, field.GetValue() :?> (Hobbes.DSL.Statements list)
       )
        