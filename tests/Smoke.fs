namespace Hobbes.Tests.System

open Xunit

module Smoke = 

    [<Fact>]
    let init() =
        let status,_ = Implementation.initDb()
        Assert.NotEqual(404,status)
        Assert.NotEqual(500,status)
        //the above two are also covered by this one
        //Testing explicitly gives better error messages for those two cases
        Assert.True(status >= 200 && status < 300)
        