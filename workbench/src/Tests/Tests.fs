module Tests
open Hobbes.Server.Db

let getData() = 
    let statusCode,res = Implementation.data "gandalf_1"
    res.Substring(0, min 500 res.Length) |> printf "Status: %d. %A" statusCode
    
let test() = 
    getData()