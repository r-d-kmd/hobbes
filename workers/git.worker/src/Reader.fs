namespace Collector.Git
open Hobbes.Web.Log
open FSharp.Data
open Hobbes.Helpers.Environment

module Reader =

    let user = env "GIT_AZURE_USER" null
    let pwd = env "GIT_AZURE_PASSWORD" null

    type GitSource = JsonProvider<"""{
                            "name"    : "git",
                            "account" : "kmddk",
                            "project" : "klkjlkj",
                            "dataset" : "commits" }""">

    type private CommitBatch = JsonProvider<"""{
        "count": 100,
        "value": [
            {
                "commitId": "b1c561d96ff1808700b34b3ef5346e0df4bf5ed8",
                "author": {
                    "name": "Jakob Pele Leer",
                    "email": "JL@kmd.dk",
                    "date": "2020-04-21T07:17:10Z"
                },
                "committer": {
                    "name": "Jakob Pele Leer",
                    "email": "JL@kmd.dk",
                    "date": "2020-04-21T07:17:10Z"
                },
                "comment": "Merged PR 37570: Student member submitting form about end of education only registers",
                "commentTruncated": true,
                "changeCounts": {
                    "Add": 0,
                    "Edit": 5,
                    "Delete": 0
                },
                "url": "https://dev.azure.com/kmddk/2139bb34-57e3-4d7d-a6e1-1c0542a45e29/_apis/git/repositories/897458ba-1c7f-40c8-8dc0-043f083c6cd7/commits/b1c561d96ff1808700b34b3ef5346e0df4bf5ed8",
                "remoteUrl": "https://dev.azure.com/kmddk/Gandalf/_git/MemberUI/commit/b1c561d96ff1808700b34b3ef5346e0df4bf5ed8"
            }]
    }""">
    type private BranchList = JsonProvider<"""{
        "value": [{
            "name": "refs/heads/47902_fix_f_ui_nightly_tests_pt3",
            "objectId": "0ba81f799d711fd6dfda9f39d3224cc30869bc57",
            "creator": {
                "displayName": "Karolina Michon (KLM)",
                "url": "https://spsprodweu1.vssps.visualstudio.com/A5171eb22-e5ac-4dc3-a6f5-552b63c90b71/_apis/Identities/0adac0d0-283d-6c75-8654-828be949be3b",
                "_links": {
                    "avatar": {
                        "href": "https://dev.azure.com/kmddk/_apis/GraphProfile/MemberAvatars/aad.MGFkYWMwZDAtMjgzZC03Yzc1LTg2NTQtODI4YmU5NDliZTNi"
                    }
                },
                "id": "0adac0d0-283d-6c75-8654-828be949be3b",
                "uniqueName": "KLM@kmd.dk",
                "imageUrl": "https://dev.azure.com/kmddk/_api/_common/identityImage?id=0adac0d0-283d-6c75-8654-828be949be3b",
                "descriptor": "aad.MGFkYWMwZDAtMjgzZC03Yzc1LTg2NTQtODI4YmU5NDliZTNi"
            },
            "url": "https://dev.azure.com/kmddk/2139bb34-57e3-4d7d-a6e1-1c0542a45e29/_apis/git/repositories/897458ba-1c7f-40c8-8dc0-043f083c6cd7/refs?filter=heads%2F47902_fix_f_ui_nightly_tests_pt3"
        }],
        "count" : 389
    }""">
    type private CommitRecord = JsonProvider<"""{
        "count": 100,
        "value": [
            {
                "commitId": "b1c561d96ff1808700b34b3ef5346e0df4bf5ed8",
                "author": {
                    "name": "Jakob Pele Leer",
                    "email": "JL@kmd.dk",
                    "date": "2020-04-21T07:17:10Z"
                },
                "committer": {
                    "name": "Jakob Pele Leer",
                    "email": "JL@kmd.dk",
                    "date": "2020-04-21T07:17:10Z"
                },
                "comment": "Merged PR 37570: Student member submitting form about end of education only registers",
                "commentTruncated": true,
                "changeCounts": {
                    "Add": 0,
                    "Edit": 5,
                    "Delete": 0
                },
                "url": "https://dev.azure.com/kmddk/2139bb34-57e3-4d7d-a6e1-1c0542a45e29/_apis/git/repositories/897458ba-1c7f-40c8-8dc0-043f083c6cd7/commits/b1c561d96ff1808700b34b3ef5346e0df4bf5ed8",
                "remoteUrl": "https://dev.azure.com/kmddk/Gandalf/_git/MemberUI/commit/b1c561d96ff1808700b34b3ef5346e0df4bf5ed8"
            }]
    }""">
    type private Repositories = JsonProvider<"""{
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
          "size": 30778301,
          "remoteUrl": "https://kmddk@dev.azure.com/kmddk/Gandalf/_git/MemberUI",
          "sshUrl": "git@ssh.dev.azure.com:v3/kmddk/Gandalf/MemberUI",
          "webUrl": "https://dev.azure.com/kmddk/Gandalf/_git/MemberUI"
        }], 
        "count" : 13
    }""">
    type private Repository = {
        Id : string
        Name : string
        DefaultBranch : string
    }

    let private readBody = 
        function
            | Binary b -> System.Text.Encoding.ASCII.GetString b
            | Text t -> t
    
    let request account project body path  = 
        let url = sprintf "https://dev.azure.com/%s/%s/_apis/git/repositories%s?api-version=5.1" account project path
        let headers =
            [
                HttpRequestHeaders.BasicAuth user pwd
                HttpRequestHeaders.ContentType HttpContentTypes.Json
            ]       
        let resp = 
            match body with
            None ->
                Http.Request(url,
                    httpMethod = "GET",
                    silentHttpErrors = true,
                    headers = headers
                ) 
            | Some body ->
                Http.Request(url,
                    httpMethod = "POST",
                    silentHttpErrors = true,
                    headers = headers,
                    body = HttpRequestBody.TextRequest body
                ) 
        resp.StatusCode,resp.Body |> readBody

    let get account project =
        request account project None
    

    let private repositories account project = 
        let statusCode, list = 
            get account project ""
        logf "Repositories: %d %s" statusCode list

        if statusCode = 200 then 
            let parsedList = 
                list |> Repositories.Parse
            let repos = 
                parsedList.Value
                |> Seq.map(fun repo -> {Id = string repo.Id; Name = repo.Name; DefaultBranch=repo.DefaultBranch})

            assert(repos |> Seq.length = parsedList.Count)
            repos
        else
            errorf null "Error when reading repositories. Staus: %d. Message: %s" statusCode list
            Seq.empty
            
    type Commit = {
        Time : System.DateTime
        Message : string
        Author : string
    }

    let commits account project = 
        let commits = 
            repositories account project
            |> Seq.collect(fun repo -> 
                let statusCode,commits = 
                    repo.Id |> sprintf "/%s/commits" |> get account project
                logf "commits: %d %s" statusCode commits
                if statusCode = 200 then 
                    let parsedCommits = 
                       commits |> CommitRecord.Parse
                    let commits = 
                        parsedCommits.Value
                        |> Seq.map(fun commit ->
                            {
                                Time = commit.Author.Date.Date
                                Message = commit.Comment
                                Author = commit.Author.Email
                            }
                        )

                    assert(commits |> Seq.length = parsedCommits.Count)
                    commits
                else
                    errorf null "Error when reading commits of %s. Staus: %d. Message: %s" repo.Name statusCode commits
                    Seq.empty
            )
        commits
           
    type BranchData = {
        Name : string
        CreationDate : System.DateTime
        LastCommit : System.DateTime
    }

    let branches account project =
       repositories account project
       |> Seq.collect(fun repo -> 
            let statusCode,branches = 
                repo.Id |> sprintf "/%s/refs" |> get account project
            
            if statusCode = 200 then 
                let parsedBranches = 
                   branches |> BranchList.Parse
                let branches = 
                    parsedBranches.Value
                    |> Seq.filter(fun branch -> branch.Name.StartsWith "refs/heads/")
                    |> Seq.map(fun branch ->
                        let body = 
                            """{
                              "itemVersion": {
                                "versionType": "branch",
                                "version": "develop"
                              }
                            }""" |> Some
                        let statusCode,commits = 
                            repo.Id |> sprintf "/%s/commitsbatch" |> request account project body
                        
                        let creationDateLastCommit = 
                            if statusCode = 200 then
                                let parsedCommits = 
                                   commits |> CommitBatch.Parse
                                let commits = 
                                    parsedCommits.Value
                                    |> Seq.map(fun commit ->
                                        {
                                            Time = commit.Author.Date.Date
                                            Message = commit.Comment
                                            Author = commit.Author.Email
                                        }
                                    ) |> Seq.sortBy (fun c -> c.Time)
                                assert(commits |> Seq.length = parsedCommits.Count)
                                let lastCommit = 
                                    commits 
                                    |> Seq.last
                                let firstCommit = 
                                    commits
                                    |> Seq.head
                                Some(firstCommit.Time,lastCommit.Time)
                            else
                                errorf null "Error when reading commit batch of %s. Staus: %d. Message: %s" branch.Name statusCode commits
                                None
                        
                        branch.Name.Substring("ref/heads/".Length), creationDateLastCommit
                    ) |> Seq.filter(snd >> Option.isSome)
                    |> Seq.map(fun (name,creationDateLastCommit) ->
                        let creationDate,lastCommit = creationDateLastCommit.Value
                        {
                            Name = name
                            CreationDate = creationDate
                            LastCommit = lastCommit
                        }
                    )

                branches
            else
                errorf null "Error when reading commits of %s. Staus: %d. Message: %s" repo.Name statusCode branches
                Seq.empty
        )