#nowarn "3061"
namespace Readers.AzureDevOps

open FSharp.Data
open Hobbes.Web
open Hobbes.Web.RawdataTypes

module Data =
    type internal LocalDataProviderConfig = JsonProvider<"""{
                "provider" : "local",
                "id" : "lkjlkj", 
                "data" : [{
                    "prop1":2,
                    "prop2":"lkjlkj",
                    "setup" : false
                },{
                    "prop1":2,
                    "prop2":"lkjlkj",
                    "setup" : false
                }]
            }""">