namespace Hobbes.Server.Db

open Database

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
        rawdata.Views.["table"].List(TableView.Parse,
                                                  startKey = startKey,
                                                  endKey = endKey
        ) |> TableView.toTable
        


