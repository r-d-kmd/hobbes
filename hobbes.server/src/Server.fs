open Saturn
open Giraffe.Core
open Giraffe.ResponseWriters
open System
open System.IO

let port = 8085

let GetString name =
    setStatusCode 200
    >=> setBodyFromString name

let GetHelloWorld =
    setStatusCode 200
    >=> setBodyFromString "Hello World"

let apiRouter = router {
    not_found_handler (setStatusCode 200 >=> text "Api 404")
    
    getf "/%s" GetString
    get "/HelloServer" GetHelloWorld
}

let appRouter = router {
    forward "" apiRouter
}

let app = application {
    url (sprintf "http://0.0.0.0:%d/" port)
    use_router appRouter
    memory_cache
    use_gzip
}

run app
