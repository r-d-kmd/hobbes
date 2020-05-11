namespace Collector.AzureDevOps.Services

open Hobbes.Web.Routing
open Collector.AzureDevOps.Db
open Hobbes.Web.Database
open Hobbes.Web
open Hobbes.Helpers
open Hobbes.Shared.RawdataTypes
open Collector.AzureDevOps.Db.Rawdata

[<RouteArea ("/admin", false)>]
module Admin =

    let formatDBList name list =
        let stringList = 
            list
            |> Seq.map (sprintf "%A")

        let body = 
            System.String.Join(",", stringList)
            |> sprintf """{"%s" : [%s]}""" name 

        200, body  

    [<Get "/list/rawdata">]
    let listRawdata() =
        Rawdata.list() |> formatDBList "rawdata"

    [<Delete "/raw/%s">]
    let deleteRaw id =
        Rawdata.delete id       

    [<Get "/clear/rawdata">]
    let clearRawdata() =
        Rawdata.clear()  

    let createSyncDoc (config : Config.Root) (revision : string) =
        200, Rawdata.createSyncStateDocument revision config

    let private uploadDesignDocument (db : Database<CouchDoc.Root>, file) =  
        async {
            let! doc = System.IO.File.ReadAllTextAsync file |> Async.AwaitTask
            if System.String.IsNullOrWhiteSpace (CouchDoc.Parse doc).Rev |> not then failwithf "Initialization documents shouldn't have _revs %s" file
            let designDocName = System.IO.Path.GetFileNameWithoutExtension file
            let oldHash = designDocName
                          |> db.TryGetHash
            let newDoc = (doc 
                           |> String.filter(not << System.Char.IsWhiteSpace))
                           
            let newHash = hash newDoc                

            let res = 
                if oldHash.IsNone || oldHash.Value <> newHash then
                    let id = sprintf "%s_hash" designDocName
                    sprintf """{"_id": %A, "hash":%A }"""  id newHash
                    |> db.InsertOrUpdate 
                    |> ignore
                    db.InsertOrUpdate doc
                else 
                    ""
            db.CompactAndClean()
            return res
        }