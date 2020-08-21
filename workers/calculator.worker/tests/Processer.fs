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
                box 0
                box (System.DateTime(2019,5,7))
                box  "Magrethe II"
                box  true
            |]
            [|
                box  1
                box (System.DateTime(2009,3,2))
                box  "Isabella"
                box  false
            |]
            [|
                box  2
                box (System.DateTime(2008,19,6))
                box  "Frederik X"
                box true
            |]
        |] |> createRecord

    let record2 = 
        [|
            [| 
                box 3
                box (System.DateTime(2019,7,5))
                box "Elisabeth II"
                box  false
            |]
            [|
                box 4
                box (System.DateTime(2009,2,3))
                box "Harry"
                box true
            |]
            [|
                box 5
                box (System.DateTime(2008,6,19))
                box "Richard I"
                box false
            |]
        |] |> createRecord
       
    let record3 = 
        [| |] |> createRecord

    [<Fact>]
    let merge() = 
       let result = MOT.merge [|record1;record2|]
       Assert.Equal(result.RowCount,record1.RowCount + record2.RowCount)
    
    [<Fact>]
    let ``merge where one collcetion is empty``() = 
       let result = MOT.merge [|record1;record3|]
       Assert.Equal(result.RowCount,record1.RowCount)
       
       let result = MOT.merge [|record3;record1|]
       Assert.Equal(result.RowCount,record1.RowCount)