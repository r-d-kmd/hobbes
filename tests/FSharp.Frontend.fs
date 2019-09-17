namespace hobbes.tests.Fsharp

open System
open Xunit
open Hobbes.DataStructures
open Hobbes.DSL
open Hobbes.Parsing.AST
open Hobbes.Parsing
open Deedle

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

        let fooList = [1;6;4;7;9;1;5;9;4;6]
        
        seq{
            yield "Sprint", seq {for i in 1..length -> i :> IComparable}
            yield "State", seq {for i in 1..length -> states.[i % states.Length]}
            yield "Sprint Start Date", seq { for i in 1..length -> System.DateTime(2019,8,25).AddDays(float i)}
            yield "Foo", Seq.map(fun x -> x :> IComparable) fooList
            //yield "Bar", seq {for i in 1..length -> i % 2 :> IComparable } 
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

    let createExpectedSortByTable sortByColumn sortFun =
        let expectedFooColumn = testDataTable 
                                |> getColumn sortByColumn
                                |> Seq.indexed
                                |> Seq.sortBy sortFun

        let indexMap = expectedFooColumn
                       |> Seq.mapi(fun i1 (i2,_) -> (i2, i1))
                       |> Map.ofSeq             
        testDataTable
        |> Seq.map(fun (name, values) ->
           name,
           values
           |> Seq.indexed
           |> Seq.sortBy(fun (i,_) -> indexMap.[i])
           |> Seq.map(snd)
        )                                     

    [<Fact>]
    let SimpleIfExpressiont() =
        let matchState = "Completed"
        let parsedStatements = 
            create (column "Test") (If (!> "State" == matchState) (Then 1.) (Else 2.))
            |> parse
        let execute = Compile.parsedExpressions [parsedStatements] 
        let actual = 
            testDataset() 
            |> execute 
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
            testDataset() 
            |> execute 
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
        testDataset() 
        |> execute 
        |> asTable
        |> assertTablesEqual testDataTable  
    [<Fact>]
    let onlyReturnNone() =
        let statement = only (!!> "foo" == !!> "boo") |> parse
        let execute = Compile.parsedExpressions [statement]
        let actual = 
            testDataset() 
            |> execute 
            |> asTable

        assertTablesEqual (testDataTable |> Seq.map (fun (c, _) -> c, Seq.empty)) actual
    [<Fact>]
    let onlyReturnSomeString() =
        let statement = only (!> "State" == "Active") |> parse
        let execute = Compile.parsedExpressions [statement]
        let actual = 
            testDataset() 
            |> execute 
            |> asTable
            |> getColumn "State"
        let expected = seq{yield (AST.KeyType.Create 3,"Active":>IComparable)}        

        compareColumns expected actual

    [<Fact>]
    let onlyReturnSomeInt() =
        let statement = only (!> "Sprint" == 5) |> parse
        let execute = Compile.parsedExpressions [statement]
        let actual = 
            testDataset()
            |> execute 
            |> asTable
            |> getColumn "Sprint"
        let expected = seq{yield (AST.KeyType.Create 4, 5 :> IComparable)}        

        compareColumns expected actual

    [<Fact>]
    let onlyReturnSomeDateTime() =
        let date = System.DateTime(2019,8,25).AddDays(float 5)
        let statement = only (!> "Sprint Start Date" == date.ToString(Globalization.CultureInfo.CurrentCulture)) |> parse
        let execute = Compile.parsedExpressions [statement]
        let actual = 
            testDataset() 
            |> execute 
            |> asTable
            |> getColumn "Sprint Start Date"
        let expected = seq{yield (AST.KeyType.Create 4, date :> IComparable)}        

        compareColumns expected actual
    
    [<Fact>]
    let sliceColumnsNonExisting() =
        let statement = slice columns ["None"] |> parse
        let execute = Compile.parsedExpressions [statement]
        let actual =
            testDataset() 
            |> execute
            |> asTable
        let expected = seq{yield "None", Seq.empty}
        assertTablesEqual expected actual

    [<Fact>]
    let sliceColumnsOne() =
        let statement = slice columns ["Sprint"] |> parse
        let execute = Compile.parsedExpressions [statement]
        let actual =
            testDataset() 
            |> execute
            |> asTable
        let expected = seq { yield "Sprint", testDataTable |> getColumn "Sprint" }
        assertTablesEqual expected actual

    [<Fact>]
    let sliceColumnsMany() =
        let statement = slice columns ["Sprint"; "Sprint Start Date"] |> parse
        let execute = Compile.parsedExpressions [statement]
        let actual =
            testDataset() 
            |> execute
            |> asTable
        let expected = seq { yield "Sprint", testDataTable |> getColumn "Sprint"
                             yield "Sprint Start Date", testDataTable |> getColumn "Sprint Start Date" }
        assertTablesEqual expected actual

    [<Fact>]
    let sliceColumnsAll() =
        let statement = slice columns ["Sprint"; "Sprint Start Date"; "State"; "Foo"] |> parse
        let execute = Compile.parsedExpressions [statement]
        let actual =
            testDataset() 
            |> execute
            |> asTable
        let expected = seq { yield "Sprint", testDataTable |> getColumn "Sprint"
                             yield "Sprint Start Date", testDataTable |> getColumn "Sprint Start Date"
                             yield "State", testDataTable |> getColumn "State"
                             yield "Foo", testDataTable |> getColumn "Foo" }
        assertTablesEqual expected actual       



    [<Fact>]
    let sortByColumnNumericValues() =
        let statement = sort by "Foo" |> parse
        let execute = Compile.parsedExpressions [statement]
        let actual =
            testDataset() 
            |> execute
            |> asTable

        let expected = createExpectedSortByTable "Foo" (snd >> snd)
        assertTablesEqual expected actual   

    [<Fact>]
    let sortByColumnStringValues() =
        let statement = sort by "State" |> parse
        let execute = Compile.parsedExpressions [statement]
        let actual =
            testDataset() 
            |> execute
            |> asTable

        let expected = createExpectedSortByTable "State" (snd >> snd)
        assertTablesEqual expected actual   

    [<Fact>]
    let sortByColumnDateTimeValues() =
        let statement = sort by "Sprint Start Date" |> parse
        let execute = Compile.parsedExpressions [statement]
        let actual =
            testDataset() 
            |> execute
            |> asTable

        let expected = createExpectedSortByTable "Sprint Start Date" (snd >> snd)
        assertTablesEqual expected actual   

    [<Fact>]
    let indexBy() =
        let statement = index by (column "State") |> parse
        let execute =  Compile.parsedExpressions [statement]
        let actual =
            testDataset() 
            |> execute
            |> asTable
        Assert.True(false)

    [<Fact>]
    let rename() =
        let statement = rename "State" "NewName" |> parse
        let execute =  Compile.parsedExpressions [statement]
        let actual =
            testDataset() 
            |> execute
            |> asTable
        let expected = Seq.map(fun (name, rows) -> if name = "State" then ("NewName", rows) else (name, rows)) testDataTable        
        assertTablesEqual expected actual

    let reduceGroup (minMaxColumn : seq<IComparable>) op (group : seq<int * (KeyType * IComparable)>) =
        let groupList = List.ofSeq group
        let rec aux groupList current ret =
            match groupList with
            | (i, (kt, v))::rest -> let current', ret' = if op (Seq.item i minMaxColumn) current then (Seq.item i minMaxColumn), (i, (kt, v)) else current, ret
                                    aux rest current' ret'
            | []                 -> ret
        aux groupList (Seq.item 0 minMaxColumn) (groupList.[0])


    [<Fact>]
    let groupByMaxBy() =
        let statement = group by ["State"] => maxby !> "Sprint" |> parse
        let execute =  Compile.parsedExpressions [statement]
        let actual =
            testDataset() 
            |> execute
            |> asTable
        let expectedStateColumn =
            testDataTable
            |> getColumn "State"
            |> Seq.indexed
            |> Seq.groupBy (snd >> snd)
            |> Seq.map snd

        let sprintColumn =
            testDataTable
            |> getColumn "Sprint"
            |> Seq.map snd  

        let reducedExpectedStateColumn = expectedStateColumn
                                         |> Seq.map (reduceGroup sprintColumn (>))

        let indexMap = reducedExpectedStateColumn
                       |> Seq.mapi(fun i1 (i2,_) -> (i2, i1))
                       |> Map.ofSeq                  


        let expected = testDataTable
                    |> Seq.map(fun (name, values) ->
                       name,
                       values
                       |> Seq.indexed
                       |> Seq.filter(fun (i,_) -> Map.exists (fun k _ -> k = i) indexMap)
                       |> Seq.sortBy(fun (i,_) -> indexMap.[i])
                       |> Seq.map(snd)
                       ) 

        assertTablesEqual expected actual

//Selector had an error in which it only was able to parse maxby. For some reason this allowed reduction to parse minby,
//as the keyword min. This needs to be looked into!
    [<Fact>]
    let groupByMinBy() =
        let statement = group by ["State"] => minby !> "Sprint" |> parse
        let execute =  Compile.parsedExpressions [statement]
        let actual =
            testDataset() 
            |> execute
            |> asTable
        let expectedStateColumn =
            testDataTable
            |> getColumn "State"
            |> Seq.indexed
            |> Seq.groupBy (snd >> snd)
            |> Seq.map snd

        let sprintColumn =
            testDataTable
            |> getColumn "Sprint"
            |> Seq.map snd  

        let reducedExpectedStateColumn = expectedStateColumn
                                         |> Seq.map (reduceGroup sprintColumn (<))

        let indexMap = reducedExpectedStateColumn
                       |> Seq.mapi(fun i1 (i2,_) -> (i2, i1))
                       |> Map.ofSeq                  


        let expected = testDataTable
                    |> Seq.map(fun (name, values) ->
                       name,
                       values
                       |> Seq.indexed
                       |> Seq.filter(fun (i,_) -> Map.exists (fun k _ -> k = i) indexMap)
                       |> Seq.sortBy(fun (i,_) -> indexMap.[i])
                       |> Seq.map(snd)
                       ) 

        assertTablesEqual expected actual    

    [<Fact>]
    let pivotSprintStateWorkitemId() =
        let frame = testDataTable
                   |> Seq.map(fun (columnName,values) -> columnName, 
                                                         values |> series)
                   |> series
                   |> Frame.ofColumns
        let x = frame.PivotTable("Sprint", "State", Frame.countRows)
        Assert.True(false)