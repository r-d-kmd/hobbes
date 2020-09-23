namespace Hobbes.Tests

open Xunit
open Hobbes.DSL
open Hobbes.Parsing.AST
open Hobbes.Parsing
open FParsec.CharParsers

module Parser =

    let parse =
        string
        >> StatementParser.parse
        >> Seq.exactlyOne

    let parseBlocks (text : string) = 
        let errors,blocks = 
            text |> BlockParser.parse
        if errors |> List.isEmpty |> not then
           printfn "Block parse error: %s" (System.String.Join(",", errors))
        Assert.Empty(errors)
        blocks


    let parseComment (text : string) = 
        match text.TrimStart()
              |> run BlockParser.commentBlock with
        Failure(msg,e,_) ->
            printf "Line: %d, Col: %d\t %s" e.Position.Line e.Position.Column msg
            Assert.True false
            ""
        | Success(Comment cmt,_,_) ->
            cmt
        | block -> 
            printf "Unexpected block %A" block
            Assert.True false
            ""


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

    [<Fact>]
    let onlyParse() =
        let actual = only (!> "Sprint" == 5) |> parse
        let expected = FilterAndSorting (Only(Comparison(ColumnName "Sprint", Number (Int32 5), EqualTo)))
        Assert.True(actual.Equals(expected))

    [<Fact>]
    let parseCommentBlock() = 
        let originalComment = 
            """# This is a markdown title
this is then the body of the paragraph


"""
        let cmt = 
            originalComment 
            |> parseComment
        Assert.Equal(originalComment.TrimEnd(),cmt)
    [<Fact>]
    let commentAndStatement() =
        let originalComment = 
            """# This is a markdown title
this is then the body of the paragraph



"""
        let statements = (only (NumberConstant(1.) == NumberConstant(1.)))::[(slice columns ["col1"; "col2"])]
        let input = 
            originalComment + 
             (System.String.Join(System.Environment.NewLine,
                                    statements
                                    |> List.map string))
        let blocks =
            input
            |> parseBlocks
            
        let cmt = 
            match blocks with
            | (Comment cmt)::[Statements [
                        FilterAndSorting(
                            AST.Only(
                                AST.Comparison(
                                    AST.Number (AST.Int32 1),
                                    AST.Number (AST.Int32 1),
                                    AST.EqualTo
                                )
                            )
                        )
                        FilterAndSorting (SliceColumns ["col1"; "col2"])
                    ]
                  ] -> cmt
            | a -> 
                printfn "Failed to parse:%A" a
                Assert.True false
                ""

        Assert.Equal(originalComment.TrimEnd(),cmt)
        