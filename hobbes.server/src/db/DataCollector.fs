module DataCollector

open System
open Hobbes.Parsing.AST

let get source datasetName = 
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

      let count = 
         [1;6;4;7;9;1;5;9;4;6] |> Seq.cast<IComparable>

      let uniqueString = 
         count
         |> Seq.map(fun i -> 
             let i = i :?> int
             sprintf "%s - %d" states.[i % states.Length] i :> IComparable
         )
     
      [
         "Sprint", seq {for i in 1..length -> i :> IComparable}
         "State", seq {for i in 1..length -> states.[i % states.Length]}
         "Sprint Start Date", seq { for i in 1..length -> System.DateTime(2019,8,25).AddDays(float i)}
         "Count", count
         "Index", uniqueString
         //yield "Bar", seq {for i in 1..length -> i % 2 :> IComparable } 
      ] |> Seq.map(fun (columnName,values) -> 
           columnName, values
                       |> Seq.mapi(fun i v -> KeyType.Create i, v)
     )
   testDataTable     