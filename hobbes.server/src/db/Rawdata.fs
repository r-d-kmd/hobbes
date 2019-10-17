namespace Hobbes.Server.Db

open Cache
open FSharp.Data

module Rawdata =
    
    type private WorkItemRevisionRecord = JsonProvider<"""
        {
            "id": "id1",
            "key": 300,
            "value": 300,
            "doc":{
            "WorkItemId":3833,
            "Revision":3,
            "RevisedDate":"2016-12-22T10:56:27.87+01:00",
            "RevisedDateSK":20161222,
            "DateSK":20161222,
            "IsCurrent":false,
            "IsLastRevisionOfDay":false,
            "IsLastRevisionOfPeriod":"None",
            "AnalyticsUpdatedDate":"2018-12-11T23:28:29.2066667Z",
            "ProjectSK":"2139bb34-57e3-4d7d-a6e1-1c0542a45e29",
            "WorkItemRevisionSK":62809820,
            "AreaSK":"4d25b0a2-1e87-4f78-adc9-0129c4b99f94",
            "IterationSK":"63f90684-6c1f-4ecf-9e44-6e055cd5f5b4",
            "ChangedByUserSK":"7de04d29-b95b-4596-8cb4-8fa60f123d82",
            "CreatedByUserSK":"349548d0-2ecd-4a1f-ae89-eaf68681d6cd",
            "ChangedDateSK":20161222,
            "CreatedDateSK":20161219,
            "StateChangeDateSK":20161220,
            "InProgressDateSK":20161220,
            "Watermark":16800,
            "Title":"Manage templates",
            "WorkItemType":"Feature",
            "ChangedDate":"2016-12-22T09:22:40.967+01:00",
            "CreatedDate":"2016-12-19T11:19:08.42+01:00",
            "State":"User stories created",
            "Reason":"Moved to state User stories created",
            "Priority":2,
            "StackRank":1999997974.0,
            "ValueArea":"Business",
            "ParentWorkItemId":2536,
            "StateCategory":"Resolved",
            "InProgressDate":"2016-12-20T11:11:57.637+01:00",
            "StateChangeDate":"2016-12-20T11:11:57.637+01:00",
            "Count":1,"CommentCount":0,
            "Agile_Gandalf_Additionalclarification":false,
            "Iteration":{
                "ProjectSK":"2139bb34-57e3-4d7d-a6e1-1c0542a45e29",
                "IterationSK":"63f90684-6c1f-4ecf-9e44-6e055cd5f5b4",
                "IterationId":"63f90684-6c1f-4ecf-9e44-6e055cd5f5b4",
                "IterationName":"Gandalf",
                "Number":159,
                "IterationPath":"Gandalf",
                "IterationLevel1":"Gandalf",
                "Depth":0,"IsEnded":false
            },
            "Area": {
                "ProjectSK":"2139bb34-57e3-4d7d-a6e1-1c0542a45e29",
                "AreaSK":"4d25b0a2-1e87-4f78-adc9-0129c4b99f94",
                "AreaId":"4d25b0a2-1e87-4f78-adc9-0129c4b99f94",
                "AreaName":"PO team",
                "Number":171,
                "AreaPath":"Gandalf\\PO team",
                "AreaLevel1":"Gandalf",
                "AreaLevel2":"PO team",
                "Depth":1
            }
        }
    }""">
    let rawdata = 
        Database.Database("rawdata", CacheRecord.Parse)
          .AddView("table")
          .AddView "WorkItemRevisions"

    let keys (source : DataConfiguration.DataSource) = 
        let startKey = 
            sprintf """["%s","%s"]""" source.SourceName source.ProjectName
        let endKey = 
            sprintf """["%s","%s_"]""" source.SourceName source.ProjectName
        startKey,endKey

    let store source project recordId data  = 
        let makeJsonDoc = 
            sprintf """{
              "_id" : "%s",
              "project": "%s",
              "source": "%s",
              "timestamp": "%s",
              "data": %s
            } """ recordId project source

        ("", makeJsonDoc (System.DateTime.Today.ToShortDateString()) data)
        |> rawdata.Post 
    let InsertOrUpdate doc = 
        rawdata.InsertOrUpdate doc
    let tryLatestId (source : DataConfiguration.DataSource) =
        let startKey, endKey = keys source 
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

    let list (source : DataConfiguration.DataSource) = 
        let startKey, endKey = keys source 
        rawdata.Views.["table"].List(TableView.parse,
                                                  startKey = startKey,
                                                  endKey = endKey
        ) |> TableView.toTable
        |> Seq.map(fun (columnName,values) -> 
            columnName, values.ToSeq()
            |> Seq.map(fun (i,v) -> Hobbes.Parsing.AST.KeyType.Create i, v)
        ) |> Hobbes.FSharp.DataStructures.DataMatrix.fromTable
        
    let tryGetRev id = rawdata.TryGetRev id    


