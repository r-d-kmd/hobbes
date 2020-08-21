namespace Worker.Git
open Hobbes.Web.Log
open FSharp.Data
open Hobbes.Helpers.Environment

module Reader =

    let user = env "AZURE_TOKEN_KMDDK" null //env "GIT_AZURE_USER" null
    let pwd = env "AZURE_TOKEN_KMDDK" null //env "GIT_AZURE_PASSWORD" null
    type ErrorBody = JsonProvider<"""{"$id":"1","innerException":null,"message":"TF401175:The version descriptor <Branch: refs/heads/develop > could not be resolved to a version in the repository Gandalf","typeName":"Microsoft.TeamFoundation.Git.Server.GitUnresolvableToCommitException, Microsoft.TeamFoundation.Git.Server","typeKey":"GitUnresolvableToCommitException","errorCode":0,"eventId":3000}""">
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
    
    let request account project body path  = 
        let url = sprintf "https://dev.azure.com/%s/%s/_apis/git/repositories%s?api-version=5.1&$top=100000" account project path
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
        if resp.StatusCode = 401 then
           errorf "Not authorized for that ressource. %s:%s %s" user pwd url
        resp.StatusCode,resp |> Hobbes.Web.Http.readBody

    let get account project =
        request account project None
          
    type Commit = {
        Date : System.DateTime
        Id : string
        Author : string
        RepositoryName : string
        BranchName : string
        Project : string
    }

    let private commitsForBranch account project (repo : Repository) (branchName : string) =
        logf "Reading commits for %s - %s" repo.Name branchName
        let shortBranchName = 
            branchName.Substring("refs/heads/".Length)
            
        let body = 
            sprintf """{
              "itemVersion": {
                "versionType": "branch",
                "version": "%s"
              }
            }""" shortBranchName |> Some
        let statusCode,commits = 
            repo.Id |> sprintf "/%s/commitsbatch" |> request account project body
        
        if statusCode = 200 then
            let parsedCommits = 
               commits |> CommitBatch.Parse
            logf "Read %d commits from %s" parsedCommits.Value.Length branchName
            let commits = 
                parsedCommits.Value
                |> Seq.map(fun commit ->
                    {
                        Date = commit.Author.Date.DateTime
                        Id = commit.CommitId
                        Author = commit.Author.Email
                        RepositoryName = repo.Name
                        BranchName = branchName
                        Project = project
                    }
                ) |> Seq.sortBy (fun c -> c.Date)
            assert(commits |> Seq.length = parsedCommits.Count)
            commits
        else
            let err = ErrorBody.Parse commits
            if err.TypeKey = "GitUnresolvableToCommitException" then
                errorf "Couldn't resolve version %s for branch %s. \n%s" shortBranchName branchName commits
            else
                errorf  "Error when reading commit batch of %s. Staus: %d. Message: %s" branchName statusCode commits
            Seq.empty

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
            errorf  "Error when reading repositories. Staus: %d. Message: %s" statusCode list
            Seq.empty

    let commits account project = 
        let commits = 
            repositories account project
            |> Seq.collect(fun repo -> 
                //todo make a job for each repo plus one for uniforming the data
                if System.String.IsNullOrWhiteSpace repo.DefaultBranch then 
                    Seq.empty
                else
                    commitsForBranch account project repo repo.DefaultBranch
            )
        commits
           
    type BranchData = {
        Name : string
        IsFirstCommit : bool
        Commit : Commit
        IsLastCommit : bool
    }

    let branches account project =
       repositories account project
       |> Seq.collect(fun repo -> 
            let statusCode,branches = 
                repo.Id |> sprintf "/%s/refs" |> get account project
            
            if statusCode = 200 then 
                let parsedBranches = 
                   branches |> BranchList.Parse
                
                parsedBranches.Value
                |> Seq.filter(fun branch -> branch.Name.StartsWith "refs/heads/")
                |> Seq.map (fun branch -> 
                    async {
                        let name = branch.Name.Substring("ref/heads/".Length)
                        let commits = 
                            try
                                let commits = commitsForBranch account project repo branch.Name
                                logf "Read %d commits from %s - %s" (commits |> Seq.length) repo.Name name
                                commits
                            with e -> 
                                excf e "Error when reading commits for branch. %s" branch.Name
                                Seq.empty
                        return
                            if commits |> Seq.isEmpty then 
                                Seq.empty
                            else
                                let lastCommit = 
                                    commits 
                                    |> Seq.last
                                let firstCommit = 
                                    commits
                                    |> Seq.head
                                commits
                                |> Seq.map(fun commit ->
                                    {
                                        Name = name
                                        IsFirstCommit = (commit = firstCommit)
                                        IsLastCommit = (commit = lastCommit)
                                        Commit = commit
                                    }
                                )
                    }
                ) |> Async.Parallel
                |> Async.RunSynchronously
                |> Seq.collect id
            else
                errorf  "Error when reading branches of %s. Staus: %d. Message: %s" repo.Name statusCode branches
                Seq.empty
        )