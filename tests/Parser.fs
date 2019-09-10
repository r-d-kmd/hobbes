namespace hobbes.tests

open Xunit
open Hobbes.DSL
open Hobbes.Parsing.AST
open Hobbes.Parsing

module Parser =

    let parse stmt =
        let stmt = stmt |> string
        Parser.parse [stmt]
        |> Seq.exactlyOne

    let buildComparisonExpr expr1 expr2 oper =
        Comparison (expr1, expr2, oper)
   
    [<Fact>]
    let ``If else``() =
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
               )) |> parse

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


        