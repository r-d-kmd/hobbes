module Routing
open FSharp.Control.Tasks.V2.ContextInsensitive
open Saturn
open Giraffe
open Microsoft.AspNetCore.Http
open Hobbes.Server.Db
open Hobbes.Server.Security
open System

let private watch = 
    let w = Diagnostics.Stopwatch()
    w.Start()
    w

let private verify (ctx : HttpContext) =
        let authToken = 
            let url = ctx.GetRequestUrl()
            printfn "Requesting access to %s" url
            match Uri(url) with
            uri when String.IsNullOrWhiteSpace(uri.UserInfo) |> not ->
                uri.UserInfo |> Some
            | _ -> 
                ctx.TryGetRequestHeader "Authorization"
                
        authToken
        |> Option.bind(fun authToken ->
            if authToken |> verifyAuthToken then
                Some authToken
            else 
                None
        ) |> Option.isSome
           
let rec private execute name f : HttpHandler =
    fun next (ctx : HttpContext) ->
            task {
                let start = watch.ElapsedMilliseconds
                let code, body = f()
                let ``end`` = watch.ElapsedMilliseconds
                Log.timed name (start - ``end``)
                return! (setStatusCode code >=> setBodyFromString body) next ctx
            }

let noArgs name f = execute name f

let withArgs name f args =  
    execute name (fun () -> f args)

let withBody name f args : HttpHandler = 
    fun next (ctx : HttpContext) ->
        task {
            let! body = ctx.ReadBodyFromRequestAsync()
            let f = f body
            return! ((withArgs name (f) args) next ctx)
        } 

let withBodyNoArgs name f : HttpHandler = 
    withBody name (fun body _ -> f body) ()

let verifiedPipe = 
    pipeline {
        plug (fun next ctx -> 
                if verify ctx then
                    (setStatusCode 200) next ctx
                else
                    (setStatusCode 403 >=> setBodyFromString "unauthorized") next ctx
            )
    }


open FSharp.Quotations.Patterns
open FSharp.Quotations
let rec internal readQuotation =
    function
        PropertyGet(_,prop,_) -> 
            [prop :> System.Reflection.MemberInfo]
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
type HttpMethods = 
    Get = 1
    | Post = 2
    | Put = 3
    | Delete = 4
    | Default = 0
[<AttributeUsage(AttributeTargets.Method, 
                        Inherited = false, 
                        AllowMultiple = false)>]
type RouteHandlerAttribute(path:string, verb : HttpMethods) =
    inherit Attribute()
    member __.Path with get() = path
    member __.Verb with get() = verb
    new (path) = RouteHandlerAttribute(path,HttpMethods.Default)

    
let tryGetAttribute<'a> (m:Reflection.MemberInfo) : 'a option= 
    match m.GetCustomAttributes(typeof<'a>,false) with
    [||] -> None
    | a -> a |> Array.head :?> 'a |> Some

let hasAttribute t (m:#System.Reflection.MemberInfo) =
    m.GetCustomAttributes(t,false) |> Seq.isEmpty |> not
 
let private filterByAttribute attributeType (types : seq<#System.Reflection.MemberInfo>) =
    types |> Seq.filter(hasAttribute attributeType)

let private  getTypesWithAttribute<'a>() =
    let att = typeof<'a> 
    Reflection.Assembly.GetExecutingAssembly().GetTypes()
    |> filterByAttribute att

let private getPropertiesdWithAttribute<'a,'att> (t: System.Type) =
    let flags = Reflection.BindingFlags.Static ||| Reflection.BindingFlags.Public
    let att = typeof<'att>
    let props = t.GetProperties(flags)
    props |> filterByAttribute att
    |> Seq.collect(fun prop -> 
       prop.GetCustomAttributes(att,false)
       |> Array.map(fun a -> 
           a :?> 'att, (prop.DeclaringType.Name + "." + prop.Name, prop.GetValue(null) :?> 'a)
       )
    )

type RouterBuilder with 
    member private __.FindMethodAndPath<'a> (action: Expr<'a -> int * string>) =
        match readQuotation action with
        [method] when (method :? Reflection.MethodInfo) ->
            let method = method :?> Reflection.MethodInfo
            let att = 
                match method |> tryGetAttribute<RouteHandlerAttribute> with
                Some att -> att
                | None -> failwithf "Route handler must include route handler attribute but '%s' didn't" method.Name
            let path = att.Path
            path, method, att.Verb
        | membr -> failwithf "Don't know what to do with %A" membr 

    
    [<CustomOperation("withBody")>]
    member this.PutWithBody(state, action : Expr<string -> int * string>) : RouterState =
        let path,method,verb = this.FindMethodAndPath action
        let f body = 
            method.Invoke(null, [|body|]) :?> int * string
        match verb with
        HttpMethods.Post ->
            this.Post(state, path,(f |> withBodyNoArgs path))
        | HttpMethods.Put | HttpMethods.Default -> 
            this.Put(state, path,(f |> withBodyNoArgs path))
        | _ -> failwithf "Body is not allowed for verb : %A" verb

    member private this.GenerateRouteWithArgs state (f : 'a -> int * string) path verb = 
        let pathf = PrintfFormat<_,_,_,_,'a>(path)
        match verb with 
        HttpMethods.Get | HttpMethods.Default ->
            this.GetF(state, pathf,(f |> withArgs path))
        | HttpMethods.Post ->
            this.PostF(state, pathf,(f |> withArgs path))
        | HttpMethods.Put ->
            this.PutF(state, pathf,(f |> withArgs path))
        | HttpMethods.Delete ->
            this.DeleteF(state, pathf,(f |> withArgs path))
        | _ -> failwithf "Don't know the verb: %A" verb


    [<CustomOperation("fetch")>]
    member this.Fetch(state, action : Expr<unit -> int * string>) : RouterState =
       let path,method,_ = this.FindMethodAndPath action
       let f = 
           fun next ctx ->
                let status,body =  (method.Invoke(null, [||]) :?> (int * string))
                (setStatusCode status >=> setBodyFromString body) next ctx
               
              
       this.Get(state,path,f)

    [<CustomOperation("withArg")>]
    member this.PutWithArg(state, action : Expr<'a -> int * string>) : RouterState =
        let path,method,verb = this.FindMethodAndPath action
        let f (args : 'a) = 
            (method.Invoke(null, [|args|]) :?> (int * string))
        this.GenerateRouteWithArgs state f path verb
        
    [<CustomOperation("withArgs")>]
    member this.PutWithArgs(state, action : Expr<('a * 'b) -> int * string>) : RouterState =
        let path,method,verb = this.FindMethodAndPath action
        let f (arg1 : 'a, arg2 : 'b) = 
            try
                (method.Invoke(null, [|arg1;arg2|]) :?> (int * string))
            with e ->
                printfn "Invocation failed: %s. Method name: %s. Parameters: %s " e.Message method.Name (System.String.Join(",",method.GetParameters() |> Array.map(fun p -> p.Name)))
                reraise()
        this.GenerateRouteWithArgs state f path verb
    
    [<CustomOperation("withArgs3")>]
    member this.PutWithArgs3(state, action : Expr<('a*'b*'c) -> int * string>) : RouterState =
        let path,method,verb = this.FindMethodAndPath action
        let f (arg1 : 'a, arg2 : 'b, arg3 : 'c) = 
            (method.Invoke(null, [|arg1;arg2;arg3|]) :?> (int * string))
        this.GenerateRouteWithArgs state f path verb