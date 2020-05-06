namespace Hobbes.Web

open FSharp.Data

module Http =
    type Response<'T> = 
        Success of 'T
        | Error of int * string

    let readBody = 
        function
            | Binary b -> System.Text.Encoding.ASCII.GetString b
            | Text t -> t
            
    let readResponse parser (resp : HttpResponse) = 
        if resp.StatusCode <> 200 then
            Error(resp.StatusCode,resp.Body |> readBody)
        else
           resp.Body
           |> readBody
           |> parser
           |> Success
    
    let get serviceName parser path = 
        let url = sprintf "http://%s-svc:8085%s" serviceName path
        Log.logf "Getting %s" url
        Http.Request(url,
                     httpMethod = "GET"
        ) |> readResponse parser

    let private putOrPost parser httpMethod serviceName path body = 
        let url = sprintf "http://%s-svc:8085%s" serviceName path
        Log.logf "%sting to %s" httpMethod url
        Http.Request(url,
                     httpMethod = httpMethod,
                     body = TextRequest body
        ) |> readResponse parser

    let put = putOrPost id "PUT"
    let post serviceName parser path body = putOrPost parser "POST" serviceName path body