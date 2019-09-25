module Tests

open System
open Xunit
open Hobbes.DSL
open Hobbes.Parsing.AST
open Hobbes.Parsing


if not(Diagnostics.Debugger.IsAttached) then
      printfn "Please attach a debugger, PID: %d" (Diagnostics.Process.GetCurrentProcess().Id)
while not(Diagnostics.Debugger.IsAttached) do
  Threading.Thread.Sleep(100)

let inline brk() = 
    if Diagnostics.Debugger.IsAttached then 
        Diagnostics.Debugger.Break()

let compile stmt =
    let stmt = stmt |> string
    Hobbes.Parsing.Parser.parse [stmt]
    |> Seq.exactlyOne

let buildComparisonExpr expr1 expr2 oper =
    Comparison (expr1, expr2, oper)

let testDataset = 
    let length = 10 //number of rows in the test data set

    //various states for the [State] column
    let states = [
        "Ready"
        "Ready for estimate"
        "New"
        "In sprint"
        "Active"
        "Completed"
        "Resolved"
    ]

    let data =
        [
            "Sprint", [for i in 1..length -> i :> IComparable]
            "State", [for i in 1..length -> states.[i % states.Length]]
            "Sprint Start Date", [for i in 1..length -> DateTime(2019,8,25).AddDays(float i)]
        ]

    printfn "*********************TEST DATA****************"
    printfn "%A" data
    printfn "**********************************************"

    data
    |> Seq.map(fun (columnName,values) -> 
        columnName, values
                    |> Seq.mapi(fun i v -> KeyType.Create i, v)
    ) |> Hobbes.DataStructures.DataMatrix.fromTable

let assertTablesEqual t1 t2 =
    List.iter2 (fun (n1, v1) (n2, v2) -> Assert.Equal(n1, n2) |> ignore
                                         List.iter2 (fun (v1 : KeyType * string) (v2 : KeyType * string) -> Assert.Equal(v1,v2)) v1 v2) t1 t2                                     
    
[<Fact>]
let ``If else parse``() =
    let compareState s =
        buildComparisonExpr (ColumnName "State") (String s) EqualTo
    let boolOr lhs rhs =
        Or (lhs, rhs)
    let state = !> "State"

    let ast = 
        create (column "ProgressState") (If ((state == "Ready") .|| (state == "Ready for estimate") .|| (state == "New"))
           (Then "Todo")
           (Else 
                (If ((state == "In sprint") .|| (state == "Active" ))
                    (Then "Doing")
                    (Else "Done"))
           )) |> compile

    let expected =
        let condition = 
            compareState "New"
            |> boolOr (
               compareState "Ready for estimate"
               |> boolOr (compareState "Ready")
            )
        let thenBody = String ("Todo")


        let elseBody =
            let condition = 
                compareState "Active"
                |> boolOr (compareState "In sprint")
            let thenBody = String "Doing"
            let elseBody = String "Done"
            IfThisThenElse(condition, thenBody, elseBody)
    
        CreateColumn (IfThisThenElse(condition, thenBody, elseBody), "ProgressState") 
        |> Column
        
    Assert.Equal(ast, expected)


    