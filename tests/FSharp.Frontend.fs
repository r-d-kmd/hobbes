namespace hobbes.tests.Fsharp

open System
open Xunit
open Hobbes.DSL
open Hobbes.Parsing.AST
open Hobbes.Parsing

module Frontend =

    let parse stmt =
        let stmt = stmt |> string
        Parser.parse [stmt]
        |> Seq.exactlyOne

    let buildComparisonExpr expr1 expr2 oper =
        Comparison (expr1, expr2, oper)
    let testDataTable =
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
        
        seq{
            yield "Sprint", seq {for i in 1..length -> i :> IComparable}
            yield "State", seq {for i in 1..length -> states.[i % states.Length]}
            yield "Sprint Start Date", seq { for i in 1..length -> DateTime(2019,8,25).AddDays(float i)}
        } |> Seq.map(fun (columnName,values) -> 
            columnName, values
                        |> Seq.mapi(fun i v -> KeyType.Create i, v)
        )
    type Column = seq<KeyType * IComparable>
    type Table = seq<string * Column>
    let testDataset() = 
        testDataTable
         |> Hobbes.DataStructures.DataMatrix.fromTable

    let asTable (matrix : Hobbes.DataStructures.IDataMatrix) : seq<string * seq<KeyType * IComparable>> = 
        (matrix :?> Hobbes.DataStructures.DataMatrix).AsTable()

    let getColumn name = 
        Map.ofSeq
        >> Map.find name

    let compareColumns expected (actual : Column) = 
        Assert.Equal(expected |> Seq.length, actual |> Seq.length)
        actual
        |> Seq.iter2(fun (rowKeyExpected,rowValueExpected) (rowKeyActual,rowValueActual) -> 
            Assert.True(rowKeyExpected.Equals rowKeyActual)
            Assert.Equal(rowValueExpected,rowValueActual)
        ) expected 

    let assertTablesEqual (expected : Table) (actual : Table) =
        Assert.Equal(expected |> Seq.length, actual |> Seq.length)
        Seq.iter2 (fun (n1, values1) (n2, values2) -> 
            Assert.Equal(n1, n2)
            compareColumns values1 values2
        ) expected actual                                     

    [<Fact>]
    let SimpleIfExpressiont() =
        let matchState = "Completed"
        let parsedStatements = 
            create (column "Test") (If (!> "State" == matchState) (Then 1.) (Else 2.))
            |> parse
        let execute = Compile.parsedExpressions [parsedStatements] 
        let actual = 
            (testDataset() |> execute) 
            |> asTable
            |> getColumn "Test"
        let expected = 
            testDataTable
            |> getColumn "State"
            |> Seq.map(fun (key,state) -> key,(if (state |> string) = matchState then 1. else 2.) :> IComparable)
        compareColumns expected actual
    [<Fact>]
    let NestedIfExpression() =
        let matchState = "Completed"
        let nestedMatchState = "Ready"
        let parsedStatements = 
            create (column "Test") (If (!> "State" == matchState) (Then 1.) (Else (If (!> "State" == nestedMatchState) (Then 2.) (Else 3.))))
            |> parse
        let execute = Compile.parsedExpressions [parsedStatements] 
        let actual = 
            (testDataset() |> execute) 
            |> asTable
            |> getColumn "Test"  
        let expected = 
            testDataTable
            |> getColumn "State"
            |> Seq.map(fun (key,state) -> key,(if (state |> string) = matchState then 1. elif (state |> string) = nestedMatchState then 2. else 3.) :> IComparable)
        compareColumns expected actual
    [<Fact>]
    let onlyReturnAll() =
        let statement = only (!!> "foo" == !!> "foo") |> parse
        let execute = Compile.parsedExpressions [statement]
        (testDataset() |> execute) 
        |> asTable
        |> assertTablesEqual testDataTable  
    [<Fact>]
    let onlyReturnNone() =
        let statement = only (!!> "foo" == !!> "boo") |> parse
        let execute = Compile.parsedExpressions [statement]
        let actual = 
            (testDataset() |> execute) 
            |> asTable

        assertTablesEqual (testDataTable |> Seq.map (fun (c, _) -> c, Seq.empty)) actual
    [<Fact>]
    let onlyReturnSome() =
        let statement = only (!> "State" == "Active") |> parse
        let execute = Compile.parsedExpressions [statement]
        let actual = 
            (testDataset() |> execute) 
            |> asTable
            |> getColumn "State"
        let expected = seq{yield (AST.KeyType.Create 3,"Active":>IComparable)}        

        compareColumns expected actual

    [<Fact>]
    let onlyReturnSomeInts() =
        let statement = only (!> "Sprint" == 5) |> parse
        let execute = Compile.parsedExpressions [statement]
        let actual = 
            (testDataset() |> execute) 
            |> asTable
            |> getColumn "Sprint"
        let expected = seq{yield (AST.KeyType.Create 4, 5 :> IComparable)}        

        compareColumns expected actual
