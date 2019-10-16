module Hobbes.DSL

open Hobbes.Parsing

let private culture = System.Globalization.CultureInfo.CurrentCulture

let toString = 
    function
      | AST.Count -> "count"
      | AST.Sum -> "sum"
      | AST.Median -> "median"
      | AST.Mean -> "mean"
      | AST.StdDev -> "stddev"
      | AST.Variance -> "variance"
      | AST.Max -> "max"
      | AST.Min -> "min"

type Expression = 
    Identifier of string
    | TextLiteral of string
    | Expanding of AST.Reduction  * Expression
    | Subtraction of Expression * Expression
    | Equal of Expression * Expression
    | If of Expression * Expression * Expression
    | Or of Expression * Expression
    | And of Expression * Expression
    | Gt of Expression * Expression
    | NumberConstant of float
    | DateTimeConstant of System.DateTime
    | Not of Expression
    | Keys
    with override x.ToString() =
           match x with
             Identifier s -> sprintf """ "%s" """ s
             | Expanding (r,exp) ->
                 sprintf "expanding %s [%s]"  (toString(r)) (exp.ToString())
             | Subtraction(a,b) -> sprintf " %s - %s" (a.ToString()) (b.ToString())
             | TextLiteral s -> sprintf " '%s' " s
             | Equal(a,b) -> sprintf " %s = %s" (a.ToString()) (b.ToString())
             | If(condition, thenBody, elseBody) ->
                 sprintf " if [%s] {%s} else {%s}" (condition.ToString()) (thenBody.ToString()) (elseBody.ToString())
             | Or(a,b) -> sprintf " (%s) || (%s) "  (a.ToString()) (b.ToString())
             | And(a,b) -> sprintf " (%s) && (%s) "  (a.ToString()) (b.ToString())
             | NumberConstant i -> i |> string
             | Gt(a,b) ->  sprintf " %s > %s" (a.ToString()) (b.ToString())
             | Not e -> sprintf "!%s" (e.ToString())
             | Keys -> "keys"
             | DateTimeConstant d -> sprintf "\'%s\'" (d.ToString(culture))
           |> sprintf "(%s)" 
         static member private ParseStringOrDate (stringOrDate : string) = 
            match System.DateTime.TryParse(stringOrDate) with
            true, v  -> DateTimeConstant v
            | false, _ -> TextLiteral stringOrDate
         static member (-) (e1:Expression, e2:Expression) = 
             Subtraction(e1,e2)
         static member (==) (e1:Expression, e2:string) = 
             let e2 = Expression.ParseStringOrDate e2
             Equal(e1,e2)
         static member (==) (e1:Expression, e2:Expression) = 
             Equal(e1,e2)
         static member (==) (e1:Expression, e2:int) = 
             Equal(e1,e2 |> float |> NumberConstant)      
         static member (!=) (e1:Expression, e2:string) = 
             let e2 = Expression.ParseStringOrDate e2
             Not(e1 == e2)
         static member (.||) (exp1:Expression,exp2:Expression) =
             Or(exp1,exp2)
         static member (.&&) (exp1:Expression,exp2:Expression) =
             And(exp1,exp2)
         static member (.>) (exp1:Expression,exp2:Expression) =
             Gt(exp1,exp2)
         static member (.>) (exp1:Expression,exp2:int) =
             Gt(exp1,exp2 |> float |> NumberConstant)
         static member (.>) (exp1:int,exp2:Expression) =
             Gt(exp1 |> float |> NumberConstant, exp2)
type Selector = 
    MaxBy of Expression
    | MinBy of Expression
    with override x.ToString() =
            let s,e = 
                match x with
                MaxBy e -> "maxby",e
                | MinBy e -> "minby",e 
            sprintf "%s %s" s (e.ToString())

type  Grouping = 
    Simple of columnList: string list * reduction : AST.Reduction
    | RowSelection of columnList: string list * selector : Selector

type ColumnsOrRows =
     Rows
     | Columns
type Statements = 
    GroupStatement of Grouping
    | CreateColumn of name:string * expression:Expression
    | Rename of string * string
    | Pivot of Expression * Expression * AST.Reduction * Expression
    | Slice of ColumnsOrRows * string list
    | Dense of ColumnsOrRows
    | Only of Expression
    | Sort of string
    | Index of Expression
    with override x.ToString() = 
           match x with
           GroupStatement grp ->
               let formatColumns columns = 
                      let columns = 
                          System.String.Join(" ", 
                              columns
                              |> List.map(sprintf """ "%s" """))
                      sprintf "group by %s -> %s" columns
               match grp with
               Simple (columns,reduction) ->
                  columns |> formatColumns <| (reduction |> toString)
               | RowSelection(columns, selector)  ->
                  let grp = columns |> formatColumns
                  let sel = selector.ToString()
                  grp sel
           | CreateColumn(name,exp) ->
              sprintf """create column "%s" (%s)""" name (exp.ToString())
           | Rename(orgColumn,newColumn) ->
                sprintf """rename column "%s" "%s" """ orgColumn newColumn
           | Pivot(exp1,exp2,r, exp3) ->
              sprintf "pivot [%s] [%s] -> %s [%s]" (exp1.ToString()) (exp2.ToString()) (r |> toString) ((exp3.ToString()))
           | Slice(Rows,_) -> "slice rows"
           | Slice(Columns,columns) -> 
               System.String.Join(" ", columns |> List.map (sprintf """ "%s" """))
               |> sprintf "slice columns %s"
           | Dense(Rows) -> "dense rows"
           | Dense(Columns) -> "dense columns"
           | Sort(name) -> sprintf """sort by column "%s" """ name
           | Index(exp) -> sprintf """index rows by %s """ (exp.ToString())
           | Only exp -> sprintf "only %s" (exp.ToString())
               
let by = ()

type GroupByWithColumnNames = GroupByWithColumnNames of string list
    with static member (=>) (grouping:GroupByWithColumnNames,r : AST.Reduction) = 
             let columnNames = 
                 match grouping with
                 GroupByWithColumnNames names -> names
             Simple(columnNames, r)  |> GroupStatement
         static member (=>) (grouping:GroupByWithColumnNames,r : Selector) = 
             let columnNames = 
                 match grouping with
                 GroupByWithColumnNames names -> names
             RowSelection(columnNames, r) |> GroupStatement

let group _ (columnNames : string list) = 
    GroupByWithColumnNames columnNames

let expanding reduction expression = 
    Expanding(reduction,expression)

let maxby expression =
    MaxBy expression

let minby exp = MinBy exp

let column name = name
let create column exp = CreateColumn(column, exp)
let rename orgColumn newColumn = Rename(orgColumn,newColumn)
let pivot exp1 exp2 reduction exp3 =
    Pivot(exp1,exp2,reduction, exp3)
let columns = Columns
type Else(expression: Expression) = 
    member x.Expressoin with get() = expression
    new(number: int) = 
        Else(number |> float |> NumberConstant)
    new(number: float) = 
        Else(number |> NumberConstant)
    new(literal : string) = 
        Else(TextLiteral(literal))
type Then = Else
let If condition (thenBody : Else) (elseBody : Else) =
   If(condition, thenBody.Expressoin, elseBody.Expressoin)
let rows = Rows
let dense = Dense
let slice colOrRow columnNames = 
    Slice(colOrRow,columnNames)
let sort _ name = 
    Sort(name)
let index _ _ exp =
    Index(exp)
let only expression = 
    Only(expression)
let inline (!!>) (text:string) = 
         TextLiteral text
let inline (!>) (identifier:string) = 
         Identifier identifier

