namespace Collector.Git.Services

open Hobbes.Server.Routing
open Hobbes.Helpers.Environment

[<RouteArea ("/data", false)>]
module Data =
    let inline private toCsList (lines : seq<string>) = 
        System.String.Join(",",lines)

    type Config = FSharp.Data.JsonProvider<"""{
        "source" : "git",
        "dataset" : "commits",
        "urls" : ["hkjh","ljlkhj"],
        "credentials" : {
            "user" : "lll",
            "pwd" : "jjj"
        }
    }
    """>
    [<Post ("/sync", true)>]
    let sync confDoc =
       let conf = Config.Parse confDoc
       match env conf.Credentials.User null, env conf.Credentials.Pwd null with
       null, _ | _, null -> 403, "Not authorized for repository"
       | user, pwd ->
           conf.Urls |> Seq.map (fun url ->
               async {
                   Collector.Git.Reader.sync user pwd url
               }
           ) |> Async.Parallel
           |> Async.RunSynchronously 
           |> ignore
           200,"Synced"

    [<Post ("/read", true)>]
    let read confDoc =
        let conf = Config.Parse confDoc
        let columnNames,rows =
            match conf.Dataset.ToLower() with
            "commits" -> 
                [
                    "timestamp"
                    "message"
                    "author"
                ],  conf.Urls
                    |> Seq.collect Collector.Git.Reader.commits
                    |> Seq.map(fun commit ->
                        commit.Author.When.ToLocalTime().ToString()::commit.MessageShort::[commit.Author.Email]
                    )
            | "branches" -> 
                [
                    "TreeName"
                    "BranchName"
                    "LifeTimeInHours"
                ], conf.Urls
                   |> Seq.collect Collector.Git.Reader.branches
                   |> Seq.map(fun branch ->
                       branch.TreeName::branch.Name::[branch.LifeTimeInHours.ToString()]
                   )
            | _ -> failwith "unknown dataset"
        let values = 
           toCsList (rows
                    |> Seq.map(toCsList >> sprintf "[%s]"))
        let res = 
            sprintf """{
                "columnNames": [%s] 
                "values" : [%s]
            } """ (toCsList columnNames) values
        200,res