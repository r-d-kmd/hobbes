namespace Hobbes.Tests

open Hobbes.Server.Security
open Xunit

module Server = 
    [<Fact>]
    let ``parse azure oauth token user``() =
       let user = 
           """eyJFbWFpbCI6InJzbEBrbWQuZGsifQ==|1570027282|-eeJmhPmhI-m8EZ45MFewOf5u1o="""
           |> tryParseUser
       Assert.True(user.IsSome)
       Assert.Equal("rsl", user.Value |> fst)

    [<Fact>]
    let ``encrypt round trip``() =
        let text = "foo bar"
        let encrypted  = text |> encrypt
        let decrypted = encrypted |> decrypt
        Assert.False((text = encrypted))
        Assert.Equal(text, decrypted)   