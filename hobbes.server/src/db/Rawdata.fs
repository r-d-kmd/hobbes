module Rawdata
open FSharp.Data
open Database

let private json2columns (data:string) = 
   seq {
       yield "some column", Seq.empty
   }

let store id (data : string)  = 
    let makeJsonDoc = sprintf """{
                          "_id" : "%s",
                          "TimeStamp" : "%s",
                          "Data" : %s
                      }"""

    makeJsonDoc id (System.DateTime.Today.ToShortDateString()) data
    |> rawdata.Put id
    (rawdata.Get id).JsonValue.ToString JsonSaveOptions.None; //TODO: Unsure if this is the right format for the string.
    


