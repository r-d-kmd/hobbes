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
        Http.Request(sprintf "http://%s-svc:8085%s" serviceName path,
                     httpMethod = "GET"
        ) |> readResponse parser
    
    let put serviceName path body = 
        Http.Request(sprintf "http://%s-svc:8085%s" serviceName path,
                     httpMethod = "PUT",
                     body = HttpRequestBody.TextRequest body
        ) |> readResponse id