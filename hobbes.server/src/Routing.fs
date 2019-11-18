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

[<AttributeUsage(AttributeTargets.Class, 
                        Inherited = false, 
                        AllowMultiple = false)>]
type RouteAreaAttribute(path : string, shouldAuthenticate : bool) = 
    inherit Attribute()
    member __.Path with get() = path
    member __.ShouldAuthenticate with get() = shouldAuthenticate
    new(path) = RouteAreaAttribute(path, true)

[<AbstractClass>] 
[<AttributeUsage(AttributeTargets.Method, 
                        Inherited = false, 
                        AllowMultiple = false)>]
type RouteHandlerAttribute internal (path:string, verb : HttpMethods, result : string, description : string) =
    inherit Attribute()
    member __.Path with get() = path
    member __.Verb with get() = verb
    member this.HasBody 
        with get() = 
            this.Body |> isNull |> not
    abstract Body : string with get

[<AttributeUsage(AttributeTargets.Method, 
                        Inherited = false, 
                        AllowMultiple = false)>]
type GetAttribute(path : string, result, description) = 
    inherit RouteHandlerAttribute(path, HttpMethods.Get, result, description)
    new(path) = GetAttribute(path,null, null)
    override __.Body with get() = null

[<AttributeUsage(AttributeTargets.Method, 
                        Inherited = false, 
                        AllowMultiple = false)>]
type PutAttribute(path : string, body : string, result, description) = 
    inherit RouteHandlerAttribute(path, HttpMethods.Put, result, description)
    new(path, body) = PutAttribute(path,body, null, null)
    override __.Body with get() = body

[<AttributeUsage(AttributeTargets.Method, 
                        Inherited = false, 
                        AllowMultiple = false)>]
type PostAttribute(path : string, body : string, result, description) = 
    inherit RouteHandlerAttribute(path, HttpMethods.Post, result, description)
    new(path, body) = PostAttribute(path,body, null, null)
    override __.Body with get() = body 

[<AttributeUsage(AttributeTargets.Method, 
                        Inherited = false, 
                        AllowMultiple = false)>]
type DeleteAttribute(path : string, result, description) = 
    inherit RouteHandlerAttribute(path, HttpMethods.Delete, result, description)
    new(path) = DeleteAttribute(path, null, null)
    override __.Body with get() = null

let tryGetAttribute<'a> (m:Reflection.MemberInfo) : 'a option= 
    match m.GetCustomAttributes(typeof<'a>,false) with
    [||] -> None
    | a -> a |> Array.head :?> 'a |> Some

let hasAttribute t (m:#Reflection.MemberInfo) =
    m.GetCustomAttributes(t,false) |> Seq.isEmpty |> not
 
let private filterByAttribute attributeType (types : seq<#System.Reflection.MemberInfo>) =
    types |> Seq.filter(hasAttribute attributeType)

let private  getTypesWithAttribute<'a>() =
    let att = typeof<'a> 
    Reflection.Assembly.GetExecutingAssembly().GetTypes()
    |> filterByAttribute att

let private getMethodsWithAttribute<'att> (t: System.Type) =
    let flags = Reflection.BindingFlags.Static ||| Reflection.BindingFlags.Public
    let att = typeof<'att>
    let props = t.GetMethods(flags)
    props |> filterByAttribute att
    |> Seq.collect(fun prop -> 
       prop.GetCustomAttributes(att,false)
       |> Array.map(fun a -> 
           a :?> 'att, prop
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

    member private __.SafeCall (method : Reflection.MethodInfo) (args : obj []) = 
        try
            method.Invoke(null, args) :?> (int * string)
        with e ->
            Log.debugf "Invocation failed: %s. Method name: %s. Parameters: %s " e.Message method.Name (System.String.Join(",",method.GetParameters() |> Array.map(fun p -> p.Name)))
            500, "Invocation error"

    member private this.GenerateRouteWithArgs state (f : 'a -> int * string) path verb = 
        let pathf = PrintfFormat<_,_,_,_,'a>(path)
        match verb with 
        HttpMethods.Get ->
            this.GetF(state, pathf,(f |> withArgs path))
        | HttpMethods.Post ->
            this.PostF(state, pathf,(f |> withArgs path))
        | HttpMethods.Put ->
            this.PutF(state, pathf,(f |> withArgs path))
        | HttpMethods.Delete ->
            this.DeleteF(state, pathf,(f |> withArgs path))
        | _ -> failwithf "Don't know the verb: %A" verb
    
    member this.LocalFetch(state, path, method : Reflection.MethodInfo) = 
        let f = 
           fun next ctx ->
                let status,body = 
                    this.SafeCall method [||]
                (setStatusCode status >=> setBodyFromString body) next ctx

        this.Get(state,path,f)

    member private this.LocalWithArg(state, path, verb, method : Reflection.MethodInfo) = 
        let f (arg1 : 'a) = 
            this.SafeCall method [|arg1|]
        this.GenerateRouteWithArgs state f path verb

    member private this.LocalWithArgs(state, path, verb, method : System.Reflection.MethodInfo) = 
        let f (arg1 : 'a, arg2 : 'b) = 
            this.SafeCall method [|arg1;arg2|]
        this.GenerateRouteWithArgs state f path verb

    member private this.LocalWithArgs3(state, path, verb, method : System.Reflection.MethodInfo) = 
        let f (arg1 : 'a, arg2 : 'b, arg3 : 'c) = 
            this.SafeCall method [|arg1;arg2;arg3|]
        this.GenerateRouteWithArgs state f path verb

    member private this.LocalWithBody(state, path, verb, method) = 
        let f body = 
            this.SafeCall method [|body|]
        match verb with
        HttpMethods.Post ->
            this.Post(state, path,(f |> withBodyNoArgs path))
        | HttpMethods.Put -> 
            this.Put(state, path,(f |> withBodyNoArgs path))
        | _ -> failwithf "Body is not allowed for verb : %A" verb

    [<CustomOperation("withBody")>]
    member this.WithBody(state, action : Expr<string -> int * string>) : RouterState =
        let path,method,verb = this.FindMethodAndPath action
        this.LocalWithBody(state,path,verb,method)

    [<CustomOperation("fetch")>]
    member this.Fetch(state, action : Expr<unit -> int * string>) : RouterState =
       let path,method,_ = this.FindMethodAndPath action
       this.LocalFetch(state, path, method)

    [<CustomOperation("withArg")>]
    member this.WithArg(state, action : Expr<'a -> int * string>) : RouterState =
        let path,method,verb = this.FindMethodAndPath action
        this.LocalWithArg(state,path, verb, method)
        
    [<CustomOperation("withArgs")>]
    member this.WithArgs(state, action : Expr<('a * 'b) -> int * string>) : RouterState =
        let path,method,verb = this.FindMethodAndPath action
        this.LocalWithArgs(state,path, verb, method)
    
    [<CustomOperation("withArgs3")>]
    member this.WithArgs3(state, action : Expr<('a*'b*'c) -> int * string>) : RouterState =
        let path,method,verb = this.FindMethodAndPath action
        this.LocalWithArgs3(state,path, verb, method)

    [<CustomOperation "collect">]
    member this.Collect(state, areaPath : string) : RouterState =
        let routes, state = 
            let areas = getTypesWithAttribute<RouteAreaAttribute>() 
            let state = 
                match
                    areas
                    |> Seq.tryFind(fun a -> 
                        (a.GetCustomAttributes(typeof<RouteAreaAttribute>, false) |> Array.head :?> RouteAreaAttribute).ShouldAuthenticate
                    ) with
                None -> state
                | Some _ ->
                    this.PipeThrough(state, verifiedPipe)
            
            if areas |> Seq.isEmpty then failwithf "Found no modules for %s" areaPath
            areas
            |> Seq.collect(fun area ->
                area |> getMethodsWithAttribute<RouteHandlerAttribute>
            ), state

        routes
        |> Seq.fold(fun state (att, method) ->
            let noOfArgs = 
                //%% is a single % escaped. All other % are an argument. Split on % thus results
                //in an array with one more elements than there are %-args
                att.Path.Replace("%%", "").Split('%', StringSplitOptions.RemoveEmptyEntries).Length - 1
            let path = areaPath + att.Path
            if att.HasBody then
                this.LocalWithBody(state,path,att.Verb, method)
            else
                match noOfArgs with
                0 -> this.LocalFetch(state, path, method)
                | 1 -> this.LocalWithArg(state, path, att.Verb, method)
                | 2 -> this.LocalWithArgs(state, path, att.Verb, method)
                | 3 -> this.LocalWithArgs3(state, path, att.Verb, method)
                | _ -> failwithf "Don't know how to handle the arguments of %s" att.Path
        ) state
        