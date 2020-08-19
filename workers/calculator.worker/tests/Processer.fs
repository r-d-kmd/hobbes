namespace Hobbes.Workers.Calculator.Tests

open Xunit
open Hobbes.Web.Cache

module MOT = Hobbes.Workers.Calculator.Processer

module Processer =
    let private columnNames =  [|"integer"; "date"; "string";"bool"|]
    let createRecord values = 
        {
            ColumnNames = columnNames
            Values = values
            RowCount = values.Length
        }
    let record1 = 
        [|
            [| 
                Value.Int 0
                Value.Date(System.DateTime(2019,5,7))
                Value.Text "Magrethe II"
                Value.Boolean true
            |]
            [|
                Value.Int 1
                Value.Date(System.DateTime(2009,3,2))
                Value.Text "Isabella"
                Value.Boolean false
            |]
            [|
                Value.Int 2
                Value.Date(System.DateTime(2008,19,6))
                Value.Text "Frederik X"
                Value.Boolean true
            |]
        |] |> createRecord

    let record2 = 
        [|
            [| 
                Value.Int 3
                Value.Date(System.DateTime(2019,7,5))
                Value.Text "Elisabeth II"
                Value.Boolean false
            |]
            [|
                Value.Int 4
                Value.Date(System.DateTime(2009,2,3))
                Value.Text "Harry"
                Value.Boolean true
            |]
            [|
                Value.Int 5
                Value.Date(System.DateTime(2008,6,19))
                Value.Text "Richard I"
                Value.Boolean false
            |]
        |] |> createRecord
       
    let record3 = 
        [| |] |> createRecord

    [<Fact>]
    let merge() = 
       let result = MOT.merge [|record1;record2|]
       Assert.Equal(result.RowCount,record1.RowCount + record2.RowCount)
    
    [<Fact>]
    let ```merge where one collcetion is empty``() = 
       let result = MOT.merge [|record1;record3|]
       Assert.Equal(result.RowCount,record1.RowCount)
       
       let result = MOT.merge [|record3;record1|]
       Assert.Equal(result.RowCount,record1.RowCount)