namespace Workbench

type TransformationAttribute() =
    inherit System.Attribute()

type ConfigurationAttribute() =
    inherit System.Attribute()

module Reflection =
   let private  getTypes() = 
       System.Reflection.Assembly.GetExecutingAssembly().GetTypes()

   let private getFields (t: System.Type) =
       t.GetFields()

   let private isTransformtion (f : System.Reflection.FieldInfo) =
       f.GetCustomAttributes(typeof<TransformationAttribute>,false) |> Seq.isEmpty |> not
       
   let transformations =
       getTypes()
       |> Seq.collect (getFields >> (Seq.filter isTransformtion))
        