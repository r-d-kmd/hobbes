namespace Hobbes.Server.Db

open Database
type DataValues =
    Floats of (int * float) []
    | Texts of (int * string) []
    | DateTimes of (int * System.DateTime) []
    with member x.Length 
           with get() = 
               match x with
               Floats a -> a.Length
               | Texts a -> a.Length
               | DateTimes a -> a.Length
         member x.Append other =
            match x,other with
            Floats a1, Floats a2 -> a2 |> Array.append a1 |> Floats
            | Texts a1, Texts a2 -> a2 |> Array.append a1 |> Texts
            | DateTimes a1,DateTimes a2 -> a2 |> Array.append a1 |> DateTimes
            | _ -> failwithf "Incompatible types: %A %A" x other
         member x.ToSeq() =
            match x with
            Floats a -> 
                a |> Array.map(fun (i,v) -> i, box v)
            | Texts a ->
                a |> Array.map(fun (i,v) -> i, box v)
            | DateTimes a ->
                a |> Array.map(fun (i,v) -> i, box v)

type DataRecord = {
    Columns : string []
    Values : DataValues []
}

module Rawdata =
    let store source project recordId data  = 
        let makeJsonDoc = 
            sprintf """{
              "_id" : "%s",
              "project": "%s",
              "source": "%s",
              "timestamp": "%s",
              "data": %s
            } """ recordId project source

        makeJsonDoc (System.DateTime.Today.ToShortDateString()) data
        |> rawdata.Post ""
    let tryLatestId (datasetId : string list) =
        let startKey = 
            System.String.Join(",", datasetId |> List.map(sprintf "%A")) |> sprintf "[%s]"
        let endKey = 
            System.String.Join(",", 
                match datasetId with
                  [source;project] -> [source;project + "a"]
                  | _ -> datasetId
                |> List.map(sprintf "%A")
            ) |> sprintf "[%s]" 
        try
            let record = 
                (rawdata.Views.["WorkItemRevisions"].List(WorkItemRevisionRecord.Parse,1,
                                                         descending = true, 
                                                         startKey = startKey,
                                                         endKey = endKey
                )
                |> Array.head)
            record.Value |> Some
        with e ->
           eprintfn "Failed to get last revision. Reason: %s" e.Message
           None
    let list (datasetId : string list) = 
        let startKey = 
            System.String.Join(",", datasetId) |> sprintf "[%s]"
        let endKey = 
            System.String.Join(",", 
                match datasetId with
                [source;project] -> [source;project + "a"]
                | _ -> datasetId
            ) |> sprintf "[%s]"
        rawdata.Views.["WorkItemRevisions"].List(TableView.Parse,
                                                  startKey = startKey,
                                                  endKey = endKey
        )
        |> Array.fold(fun (count, (map : Map<_,_>)) record ->
            let values = 
                record.Values
                |> Array.map(fun raw -> 
                   match raw.Numbers with
                   [||] -> 
                       match raw.Strings with
                       [||] -> 
                           raw.DateTimes
                           |> Array.mapi (fun i dt -> i + count,dt)
                           |> DateTimes
                       | strings ->
                           strings
                           |> Array.mapi (fun i dt -> i + count,dt)
                           |> Texts
                   | numbers ->
                       numbers
                       |> Array.mapi(fun i n -> i + count, float n)
                       |> Floats
                )
            let map = 
               record.ColumnNames
               |> Array.indexed
               |> Array.fold(fun map (i,columnName) ->
                   let columnValues = values.[i]
                   match map |> Map.tryFind columnName with
                   None -> map.Add(columnName, columnValues)
                   | Some vs -> map.Add(columnName, vs.Append columnValues)
               ) map
            //Values can have empty cells in the end but needs to be aligned on the first element
            let maxLength = 
                (values
                 |> Array.maxBy(fun a -> a.Length)).Length
            count + maxLength, map
        ) (0,Map.empty)
        |> snd
        |> Map.toSeq
        


