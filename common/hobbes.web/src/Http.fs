namespace Hobbes.Web

open FSharp.Data

module Http =
    type Response<'T> = 
        Success of 'T
        | Error of int * string

    type Service = 
         Generic of string
         | UniformData
         | Calculator
         | Configurations
         | Collector of string
         with 
             override x.ToString() = 
               match x with
               Generic name -> name.ToLower().Replace(" ","")
               | UniformData -> "uniformdata"
               | Calculator -> "calculator"
               | Configurations -> "configurations"
               | Collector collectorName ->  
                   collectorName.ToLower().Replace(" ","") 
                   |> sprintf "%scollector"
             member x.ServiceUrl
                  with get() = 
                      sprintf "http://%s-svc:8085" (x.ToString())


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
    
    let get (service : Service) parser path = 
        let url = service.ServiceUrl + path
        Log.logf "Getting %s" url
        Http.Request(url,
                     httpMethod = "GET"
        ) |> readResponse parser

    let private putOrPost parser httpMethod (service : Service) path body = 
        let url = service.ServiceUrl + path
        Log.logf "%sting to %s" httpMethod url
        Http.Request(url,
                     httpMethod = httpMethod,
                     body = TextRequest body
        ) |> readResponse parser

    let put = putOrPost id "PUT"
    let post service parser path body = putOrPost parser "POST" service path body