namespace Hobbes.Shared

open FSharp.Data

module RawdataTypes =
    type AzureDevOpsAnalyticsRecord = JsonProvider<"""{
      "@odata.context": "https://analytics.dev.azure.com/kmddk/flowerpot/_odata/v2.0/$metadata#WorkItemRevisions(WorkItemId,WorkItemType,State,StateCategory,Iteration)",
      "timeStamp" : "ojsdfidsj",
      "value": [
        {
        "WorkItemId":3833,
        "RevisedDate":"2016-12-22T10:56:27.87+01:00",
        "IsCurrent":false,
        "IsLastRevisionOfDay":false,
        "WorkItemRevisionSK":62809820,
        "Title":"Manage templates",
        "WorkItemType":"Feature",
        "ChangedDate":"2016-12-22T09:22:40.967+01:00",
        "CreatedDate":"2016-12-19T11:19:08.42+01:00",
        "ClosedDate": "2018-09-13T16:14:24.323+02:00",
        "State":"User stories created",
        "Priority":2,
        "StateCategory":"Resolved",
        "StoryPoints": 2.0,
        "LeadTimeDays" : 200.1,
        "CycleTimeDays" : 9080.122,
        "Area": {
                "AreaPath": "Momentum"
            },
        "Iteration":{
            "IterationName":"Gandalf",
            "Number":159,
            "StartDate": "2017-05-15T00:00:00+02:00",
            "EndDate": "2017-05-28T23:59:59.999+02:00",
            "IterationPath":"Gandalf",
            "IterationLevel1":"Gandalf",
            "IterationLevel2":"Gandalf",
            "IterationLevel3":"Gandalf",
            "IterationLevel4":"Gandalf"
        }},{
        "WorkItemId":3833,
        "WorkItemRevisionSK":62809820,
        "Title":"Manage templates",
        "WorkItemType":"Feature",
        "Iteration":{
            "IterationName":"Gandalf",
            "Number":159,
            "IterationPath":"Gandalf"
        }},{
            "WorkItemId":3833,
            "WorkItemRevisionSK":62809820,
            "Title":"Manage templates",
            "WorkItemType":"Feature"
        }
    ], "@odata.nextLink":"https://analytics.dev.azure.com/"}""">

    //Helper method for optional properties of the data record
    let inline asObj v =
        match v with
        Some v -> 
            v |> box
        | None -> 
            null

    let azureFields = 
        [
             //"RevisedDate", fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> asObj row.RevisedDate
             "WorkItemId",  fun (row : AzureDevOpsAnalyticsRecord.Value) -> box row.WorkItemId 
             //"IsLastRevisionOfDay" , fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> asObj row.IsLastRevisionOfDay
             //"Title",  fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> box row.Title 
             "ChangedDate", fun (row : AzureDevOpsAnalyticsRecord.Value) -> asObj row.ChangedDate 
             "WorkItemType",  fun (row : AzureDevOpsAnalyticsRecord.Value) -> box row.WorkItemType 
             "CreatedDate", fun (row : AzureDevOpsAnalyticsRecord.Value) -> asObj row.ChangedDate 
             "ClosedDate", fun (row : AzureDevOpsAnalyticsRecord.Value) -> asObj row.ClosedDate
             "State", fun (row : AzureDevOpsAnalyticsRecord.Value) -> asObj row.State 
             "StateCategory",fun (row : AzureDevOpsAnalyticsRecord.Value) -> asObj row.StateCategory 
             //"Priority", fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> asObj row.Priority 
             "LeadTimeDays", fun (row : AzureDevOpsAnalyticsRecord.Value) -> asObj row.LeadTimeDays 
             "CycleTimeDays", fun (row : AzureDevOpsAnalyticsRecord.Value) -> asObj row.CycleTimeDays          
             //"WorkItemRevisionSK", fun (row : Rawdata.AzureDevOpsAnalyticsRecord.Value) -> box row.WorkItemRevisionSk
             "StoryPoints", fun (row : AzureDevOpsAnalyticsRecord.Value) -> asObj row.StoryPoints

        ]    