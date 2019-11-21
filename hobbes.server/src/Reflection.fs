namespace Hobbes.Server

open FSharp.Quotations.Patterns
open System.Reflection

module Reflection = 
    let tryGetAttribute<'a> (m:MemberInfo) : 'a option= 
        match m.GetCustomAttributes(typeof<'a>,false) with
        [||] -> None
        | a -> a |> Array.head :?> 'a |> Some
    let getAttribute<'a> (m:MemberInfo) =
        m
        |> tryGetAttribute<'a>
        |> Option.get
        
    let hasAttribute t (m:#MemberInfo) =
        m.GetCustomAttributes(t,false) |> Seq.isEmpty |> not
     
    let filterByAttribute attributeType (types : seq<#MemberInfo>) =
        types |> Seq.filter(hasAttribute attributeType)

    let getTypesWithAttribute<'a>() =
        let att = typeof<'a> 
        let asm = Assembly.GetExecutingAssembly()
        let assemblies =
            asm::(
                    asm.GetReferencedAssemblies()
                    |> Array.map(fun assemblyName -> 
                        Assembly.Load(assemblyName)
                    ) |> List.ofArray
                 ) |> Seq.ofList
        let types = 
            assemblies
            |> Seq.collect(fun a -> 
                a.GetTypes()
                |> Seq.ofArray
            ) 
        types
        |> filterByAttribute att

    let getMembersWithAttribute<'att> (t: System.Type) = 
        let flags = BindingFlags.Static ||| BindingFlags.Public
        let att = typeof<'att>
        let props = t.GetMembers(flags)
        props |> filterByAttribute att
        |> Seq.collect(fun prop -> 
           prop.GetCustomAttributes(att,false)
           |> Array.map(fun a -> 
               a :?> 'att, prop
           )
        )

    let getPropertiesdWithAttribute<'a,'att> t =
        t 
        |> getMembersWithAttribute<'att>
        |> Seq.filter(fun (_,m) -> m :? PropertyInfo)
        |> Seq.map(fun (a,m) -> a, m :?> PropertyInfo)
        |> Seq.collect(fun (_,prop) -> 
           prop.GetCustomAttributes(typeof<'att>,false)
           |> Array.map(fun a -> 
               a :?> 'att, (prop.DeclaringType.Name + "." + prop.Name, prop.GetValue(null) :?> 'a)
           )
        )

    let getMethodsWithAttribute<'att> (t: System.Type) =
        t 
        |> getMembersWithAttribute<'att>
        |> Seq.filter(fun (_,m) -> m :? MethodInfo)
        |> Seq.map(fun (a,m) -> a, m :?> MethodInfo)

    let rec readQuotation =
        function
            PropertyGet(_,prop,_) -> 
                [prop :> MemberInfo]
            | NewUnionCase (_,exprs) ->
                //this is also the pattern for a list
                let elements =  
                    exprs
                    |> List.collect(fun expr ->
                        (readQuotation expr)
                    )
                elements
            | Sequential(head,tail) ->
                readQuotation(head)@(readQuotation tail)
            | Lambda(_,expr) ->
                readQuotation expr
            | Call(_,method,_) -> 
                [method]
            | Let (_, _,expr) ->
                readQuotation expr
            | expr -> 
                failwithf "Didn't understand expression: %A" expr
                