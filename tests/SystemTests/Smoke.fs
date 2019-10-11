namespace Hobbes.Tests.System

open Hobbes.Server.Security
open Xunit

module Smoke = 

    let init = 
        Implementation.initDb()
        |> printfn "init %A"
        

    [<Fact>]
    let ping() = 
        //basically redundant since the init would fail if the db is not up
        Implementation.ping()
        |> snd
        |> printfn "ping -> %s" 