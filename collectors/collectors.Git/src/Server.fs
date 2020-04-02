open Saturn
open Giraffe
open Microsoft.AspNetCore.Http
open Hobbes.Helpers.Environment

let private port = env "port" "8085"
                   |> int

