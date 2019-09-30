module DataCollector

open System
open Hobbes.Parsing.AST
open Hobbes.FSharp.DataStructures
open Hobbes.Server.Db.DataConfiguration

let testDataCollector  = 
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
         states
         |> Seq.mapi(fun i state -> 
             sprintf "%s - %d" state i :> IComparable
         )
     
      [
         "Sprint", seq {for i in 1..length -> i :> IComparable}
         "State", seq {for i in 1..length -> states.[i % states.Length]}
         "Sprint Start Date", seq { for i in 1..length -> System.DateTime(2019,8,25).AddDays(float i)}
         "Count", count
         "Index", uniqueString
      ] |> Seq.map(fun (columnName,values) -> 
           columnName, values
                       |> Seq.mapi(fun i v -> KeyType.Create i, v)
     )

  
   (testDataTable
    |> DataMatrix.fromTable).ToJson()
   
let get source = 
   match source with
   Test -> testDataCollector
   | _ -> 
       failwithf "No collector for source %A" source
   