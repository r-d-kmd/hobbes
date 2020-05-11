namespace Collector.Git.Services

open Hobbes.Web.Routing
open Hobbes.Helpers.Environment

[<RouteArea ("/data", false)>]
module Data =
    let inline jsonString (s : string) = 
            "\"" +
             s.Replace("\\","\\\\") 
              .Replace("\"","\\\"") 
            + "\""
    let inline private toCsList (lines : seq<string>) = 
        System.String.Join(",",lines |> Seq.map jsonString)

    type Config = FSharp.Data.JsonProvider<"""{
        "source" : "git",
        "dataset" : "commits",
        "account" : "kmddk",
        "project" : "flowerpot",
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
       | _ ->
           200,"Synced"

    [<Post ("/read", true)>]
    let read confDoc =
        let conf = Config.Parse confDoc
        let dataset = conf.Dataset.ToLower()
        let key = confDoc |> Hobbes.Shared.RawdataTypes.keyFromConfigDoc
        let columnNames,rows =
            match dataset with
            "commits" -> 
                [
                    "timestamp"
                    "message"
                    "author"
                ],  
                    let commits = Collector.Git.Reader.commits conf.Account conf.Project
                    Hobbes.Web.Log.logf "formatting commits %A" commits
                    commits |> Seq.map(fun commit ->
                        Hobbes.Web.Log.logf "reading commit: %s" commit.Message
                        commit.Time.ToString()::commit.Message::[commit.Author]
                    )
            | "branches" -> 

                let branches = 
                    Collector.Git.Reader.branches conf.Account conf.Project
                Hobbes.Web.Log.logf "formatting branches %A" branches
                [
                    "BranchName"
                    "LifeTimeInHours"
                ], branches
                   |> Seq.map(fun branch ->
                       branch.Name::[branch.LifeTimeInHours.ToString()]
                   )
            | _ -> failwith "unknown dataset"

        let values = 
           System.String.Join(",",rows |> Seq.map (toCsList >> sprintf "[%s]"))

        let res = 
            sprintf """{
               "_id" : "%s",
               "columnNames" : [%s],
               "rows" : [%s],
               "rowCount" : %d
               }""" key (toCsList columnNames) values (rows |> Seq.length)
        Hobbes.Web.Log.logf "Result: %s" res
        200,res