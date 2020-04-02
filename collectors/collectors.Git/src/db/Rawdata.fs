#nowarn "3061"
namespace Collector.AzureDevOps.Db

open Hobbes.Server.Db.Cache
open FSharp.Data
open Hobbes.Web
open Hobbes.Server.Db
open Hobbes.Shared.RawdataTypes

module Rawdata =

    let private db = 
        Database.Database("rawdata", CacheRecord.Parse, Log.loggerInstance)
                .AddView "WorkItemRevisions"
    type CommitList = JsonProvider<"""{
    "count": 17111,
    "value": [
        {
            "commitId": "315641e7dbdeb161718619eaaba53b4be48e57ad",
            "author": {
                "name": "Carsten Mølager West Jørgensen",
                "email": "CJG@kmd.dk",
                "date": "2020-03-30T10:21:36Z"
            },
            "committer": {
                "name": "Carsten Mølager West Jørgensen",
                "email": "CJG@kmd.dk",
                "date": "2020-03-30T10:21:36Z"
            },
            "comment": "Merged PR 36284: Ny ferielov 2020",
            "commentTruncated": true,
            "changeCounts": {
                "Add": 0,
                "Edit": 1,
                "Delete": 0
            },
            "url": "https://dev.azure.com/kmddk/2139bb34-57e3-4d7d-a6e1-1c0542a45e29/_apis/git/repositories/897458ba-1c7f-40c8-8dc0-043f083c6cd7/commits/315641e7dbdeb161718619eaaba53b4be48e57ad",
            "remoteUrl": "https://dev.azure.com/kmddk/Gandalf/_git/MemberUI/commit/315641e7dbdeb161718619eaaba53b4be48e57ad"
        }]
    }"""
    type RepositoryList = JsonProvider<"""{
    "value": [
        {
            "id": "897458ba-1c7f-40c8-8dc0-043f083c6cd7",
            "name": "MemberUI",
            "url": "https://dev.azure.com/kmddk/2139bb34-57e3-4d7d-a6e1-1c0542a45e29/_apis/git/repositories/897458ba-1c7f-40c8-8dc0-043f083c6cd7",
            "project": {
                "id": "2139bb34-57e3-4d7d-a6e1-1c0542a45e29",
                "name": "Gandalf",
                "description": "Gandalf",
                "url": "https://dev.azure.com/kmddk/_apis/projects/2139bb34-57e3-4d7d-a6e1-1c0542a45e29",
                "state": "wellFormed",
                "revision": 2247,
                "visibility": "private",
                "lastUpdateTime": "2019-11-15T11:09:49.727Z"
            },
            "defaultBranch": "refs/heads/develop",
            "size": 30747608,
            "remoteUrl": "https://kmddk@dev.azure.com/kmddk/Gandalf/_git/MemberUI",
            "sshUrl": "git@ssh.dev.azure.com:v3/kmddk/Gandalf/MemberUI",
            "webUrl": "https://dev.azure.com/kmddk/Gandalf/_git/MemberUI"
        },
        {
            "id": "66301b08-c76e-4609-9898-2aa3657527b9",
            "name": "DocMerge",
            "url": "https://dev.azure.com/kmddk/2139bb34-57e3-4d7d-a6e1-1c0542a45e29/_apis/git/repositories/66301b08-c76e-4609-9898-2aa3657527b9",
            "project": {
                "id": "2139bb34-57e3-4d7d-a6e1-1c0542a45e29",
                "name": "Gandalf",
                "description": "Gandalf",
                "url": "https://dev.azure.com/kmddk/_apis/projects/2139bb34-57e3-4d7d-a6e1-1c0542a45e29",
                "state": "wellFormed",
                "revision": 2247,
                "visibility": "private",
                "lastUpdateTime": "2019-11-15T11:09:49.727Z"
            },
            "defaultBranch": "refs/heads/master",
            "size": 314026541,
            "remoteUrl": "https://kmddk@dev.azure.com/kmddk/Gandalf/_git/DocMerge",
            "sshUrl": "git@ssh.dev.azure.com:v3/kmddk/Gandalf/DocMerge",
            "webUrl": "https://dev.azure.com/kmddk/Gandalf/_git/DocMerge"
        }
    ],
    "count": 13
}""">

    //sends a http request   
    let private request user pwd httpMethod body url  =
        let headers =
            [
                HttpRequestHeaders.BasicAuth user pwd
                HttpRequestHeaders.ContentType HttpContentTypes.Json
            ]       
        match body with
        None ->
            Http.AsyncRequest(url,
                httpMethod = httpMethod, 
                silentHttpErrors = true,
                headers = headers
            ) |> Async.RunSynchronously
        | Some body ->
            Http.Request(url,
                httpMethod = httpMethod, 
                silentHttpErrors = true, 
                body = TextRequest body,
                headers = headers
            )
 
    let readData azureToken org projectName =
        let repositoriesUrl =
            sprintf """https://dev.azure.com/%s/%s/_apis/git/repositories/""" org projectName
        let req = request azureToken azureToken "GET" None
        let version = "api-version=5.1"

        let commitUrl repositoryId = 
           sprintf """%s%s/commits/?$top=100000&%s""" repositoriesUrl repositoryId version
        
        System.String.Join("\n",
            (repositoriesUrl + "?" + version
             |> req
             |> RepositoryList.Parse).Value
             |> Array.map(fun r ->
                 let commit = 
                     r.id
                     |> commitUrl
                     |> req
                     |> RepositoryList.Parse
                 sprintf "%s,%d, %d, %d" (commit.Author.Date.ToString()), commit.changeCounts.Add, commit.changeCounts.Edit,commit.changeCounts.Delete
             )) 
