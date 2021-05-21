namespace Hobbes.Workers

open Hobbes
open Hobbes.FSharp.Compile
open System.IO
open FSharp.Data
open Hobbes.Helpers
open FSharp.Data.JsonExtensions

module RestProvider = 

    type Value = Parsing.AST.Value
    type Source = 
        {
            User   : string option
            Pwd    : string option
            Url    : string []
            Meta   : (string * JsonValue) []
            Name   : string
            Values : string option
        }
    let md5Hash (input : string) =
        use md5Hash = System.Security.Cryptography.MD5.Create()
        let data = md5Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input))
        let sBuilder = System.Text.StringBuilder()
        (data
        |> Seq.fold(fun (sBuilder : System.Text.StringBuilder) d ->
                sBuilder.Append(d.ToString("x2"))
        ) sBuilder).ToString()  

    let readValue (json:JsonValue)  = 
        let rec inner json namePrefix = 
            let wrap (v : #System.IComparable) = 
              [|System.String.Join(".", namePrefix |> List.rev),v :> System.IComparable|]
            match json with
            | FSharp.Data.JsonValue.String s ->  
                match System.Double.TryParse(s) with
                true,f -> f |> wrap
                | _ -> 
                    match System.DateTime.TryParse(s) with
                    true,dt -> dt |> wrap
                    | _ -> 
                        match System.Int32.TryParse(s) with
                        true,n -> n |> wrap
                        | _ -> s |> wrap
            | FSharp.Data.JsonValue.Number d -> d |> wrap
            | FSharp.Data.JsonValue.Float f -> f |> wrap
            | FSharp.Data.JsonValue.Boolean b ->  b |> wrap
            | FSharp.Data.JsonValue.Record properties ->
                properties
                |> Array.collect(fun (name,v) ->
                    name::namePrefix |> inner v 
                )
            | FSharp.Data.JsonValue.Array elements ->
                 elements
                 |> Array.indexed
                 |> Array.collect(fun (i,v) ->
                     (string i)::namePrefix |> inner v 
                 ) 
            | FSharp.Data.JsonValue.Null -> wrap ""
        inner json []

    let read (source : Source) = 
        
        let requestData url =
            printfn "Reading data from %s" url
            try
                let json = 
                    Http.RequestString(url,
                        headers = [
                          if source.User.IsSome then yield HttpRequestHeaders.BasicAuth source.User.Value source.Pwd.Value
                        ],
                        httpMethod = "GET"
                    ) |> FSharp.Data.JsonValue.Parse
                match source.Values with
                None -> 
                    match json with
                    JsonValue.Array items -> items
                    | v -> 
                        eprintfn "Unexpected value: %A" v
                        [||]
                | Some valueProp ->
                match json.TryGetProperty valueProp with
                Some (JsonValue.Array items) -> items
                | v -> 
                    eprintfn "Unexpected value: %A" v
                    [||]
            with _ ->
              [||]
            
        let user = 
            source.User
            |> Option.map(fun user ->
                if user.StartsWith("$") then
                    env (user.Substring(1)) user
                else
                    user
            )

        let pwd = 
            source.Pwd
            |> Option.map(fun pwd ->
                if pwd.StartsWith("$") then
                    env (pwd.Substring(1)) pwd
                else
                    pwd
            )

        source.Url
        |> Seq.collect (requestData >> Array.collect (readValue))
        |> Seq.groupBy fst
        |> Seq.map(fun (columnName,cells) -> 
            columnName,cells 
                       |> Seq.mapi(fun i (_,value) ->
                           Hobbes.Parsing.AST.KeyType.Create i,value
                       )
        )