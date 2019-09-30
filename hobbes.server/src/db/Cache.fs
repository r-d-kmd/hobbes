module Cache
open FSharp.Data

let private json2columns (data:string) = 
   seq {
       yield "some column", Seq.empty
   }

let store configurationName (jsonDoc : string)  = 
    jsonDoc 

let raw configurationName data =
    (data
    |> json2columns
    |> Hobbes.FSharp.DataStructures.DataMatrix.fromTable).ToJson()
    |> store configurationName
    |> json2columns
