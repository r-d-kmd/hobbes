namespace Hobbes.OData

open Hobbes.Parsing

module Compile = 
    
    type CompileResult = {
        Filters : string option
        Fields : string option
        OrderBy : string option
    }
           
    let parsedExpressions (expressions : seq<AST.Expression>) = 
        expressions

    let rec compileComparisonExpression lhs rhs op  =
        let compiledLhs = compileExpression lhs
        let compiledRhs = compileExpression rhs

        let binary op lhs rhs =
            sprintf "%s %s %s" lhs op rhs

        let ops = 
            match op with
            AST.GreaterThan -> binary "gt"        
            | AST.GreaterThanOrEqual -> binary "ge" 
            | AST.LessThan -> binary "lt"           
            | AST.LessThanOrEqual -> binary "le"  
            | AST.EqualTo -> binary "eq"
            | AST.Contains ->
                sprintf "contains(%s,%s)"
        ops compiledLhs compiledRhs

    and compileExpression exp =
        let binary lhs rhs op = 
            let compiledLhs = compileExpression lhs
            let compiledRhs = compileExpression rhs
            let simpleBinary op lhs rhs =
                sprintf "%s %s %s" lhs op rhs

            let ops =
                match op with
                AST.Addition -> 
                    match lhs with 
                    AST.String _ -> 
                        sprintf "concat(%s,%s)"
                    | _ -> simpleBinary "add"         
                | AST.Subtraction -> simpleBinary "sub"   
                | AST.Multiplication -> simpleBinary "mul"
                | AST.Division -> simpleBinary "div"
                | AST.Modulo -> simpleBinary "mod"
            ops compiledLhs compiledRhs
        
        let exp = 
            match exp with
            AST.Binary(lhs,rhs,op) -> binary lhs rhs op
            | AST.Boolean b -> b |> string
            | AST.Number n -> 
                  match n with
                  AST.Int32 n -> string n
                  | AST.Int64 n -> string n
                  | AST.Float n -> string n
            | AST.Int(AST.Number n) -> 
                match n with
                  AST.Int32 n -> int n
                  | AST.Int64 n -> int n
                  | AST.Float n -> int n
                |> string
            | AST.Int(AST.String s) ->
                s |> int |> string
            | AST.MissingValue -> "null"
            | AST.String s -> sprintf "'%s'" s
            | AST.DateTime dt -> dt.ToString()
            | AST.ColumnName cn -> cn
            | AST.FormatDate (cn,format) ->
                let formatDate op column =
                    sprintf "%s(%s)" op column
                let func = 
                    match format with
                    AST.Year -> "year"
                    | AST.Month -> "month"
                    | AST.Day -> "day"
                    | AST.Date -> "date"
                    | AST.Week 
                    | AST.Weekday -> failwith "not supported"
                formatDate func cn
            | AST.Int _
            | AST.Keys 
            | AST.ColumnExpression _ 
            | AST.Regression _ 
            | AST.Extrapolate _
            | AST.IfThisThenElse _
            | AST.Ordinals -> failwith "Not supported for OData"
            | AST.RegularExpression _ -> failwith "Not implemented"
        sprintf "(%s)" exp

    and compileBooleanExpression c =
        let compiledExp = 
            match c with 
            AST.And (lhs,rhs) ->
                sprintf "%s and %s" (compileBooleanExpression lhs) (compileBooleanExpression rhs)
            |AST.Or(lhs,rhs) ->
                sprintf "%s or %s" (compileBooleanExpression lhs) (compileBooleanExpression rhs)
            |AST.Not(exp) ->
                exp |> compileBooleanExpression |> sprintf "not %s"
            |AST.Comparison(lhs,rhs,op) ->
                compileComparisonExpression lhs rhs op
            |AST.ValueOfColumn fieldName -> 
                       fieldName
        sprintf "( %s )" compiledExp
    let expressions (lines : #seq<string>) = 
        let rec union list1 list2 =
            match list1, list2 with
            | [], other | other, [] -> other
            | x::xs, y::_ when x < y -> x :: (union xs list2)
            | _, y::ys -> y :: (union list1 ys)

        match lines with
        l when l |> Seq.isEmpty  -> 
            {
                Fields = None
                Filters = None
                OrderBy = None
            }
        | lines ->
            let ast = Parser.parse lines
            let fields,orderBy = 
               ast 
               |> Seq.fold(fun (fields,orderBy) a ->
                  match a with
                  AST.FilterAndSorting fs ->
                      match fs with
                      AST.Only _ ->  (fields,orderBy)
                      | AST.SliceColumns a ->
                            match  fields with
                            None -> (a |> List.sort |> Some,orderBy)
                            | Some f ->
                                (f |> List.sort |> union a |> Some,orderBy)
                      | AST.DenseColumns
                      | AST.DenseRows  
                      | AST.NumericColumns
                      | AST.IndexBy _ -> failwith "Not supported"
                      | AST.SortBy name ->  
                          (fields, match orderBy with
                                   None -> [name] |> Some
                                   | Some cols -> name::cols |> Some)
                  | AST.Reduction _
                  | AST.Cluster _ 
                  | AST.Column _ -> failwith "Not implemented"
                  | AST.NoOp -> (fields,orderBy)
               ) (None,None)
            let filters =
                let f =
                    ast
                    |> Seq.fold(fun filters a ->
                        match a with
                        AST.FilterAndSorting fs ->
                            match fs with
                            AST.Only(c) ->
                                (compileBooleanExpression c)::filters
                            | AST.SliceColumns _ -> filters
                            | AST.DenseColumns
                            | AST.DenseRows  
                            | AST.NumericColumns
                            | AST.IndexBy _ -> failwith "Not supported"
                            | AST.SortBy _ -> filters
                        | AST.Reduction _
                        | AST.Cluster _ 
                        | AST.Column _ -> failwith "Not implemented"
                        | AST.NoOp -> filters
                    ) []

                match f with
                [] -> None
                | [f] -> Some f
                | fs ->
                    System.String.Join(" and ", fs) |> Some
                
            let orderBy = 
                orderBy
                |> Option.bind(fun fs ->
                    System.String.Join(",",fs |> List.rev)
                    |> Some
                )   
            let fields = 
                fields
                |> Option.bind(fun fs ->
                    System.String.Join(",",fs)
                    |> Some
                )
            {
                Fields = fields
                Filters = filters
                OrderBy = orderBy
            }
