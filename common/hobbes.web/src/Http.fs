namespace Hobbes.Web

open FSharp.Data

module Http =
    type Response<'T> = 
        Success of 'T
        | Error of int * string
    
    type CollectorService =
        Read
        | Sync
        with member x.ToPath() =
                "/data/" +
                match x with
                Read -> "read"
                | Sync -> "sync"
    type ConfigurationService =
        Configuration of string option
        | Transformation of string option
        | Source
        with member x.ToPath() =
               "/data/" +
               match x with
               Configuration s -> 
                   "configuration" + 
                     match s with
                     None -> ""
                     | Some key -> "/" + key
               | Transformation s -> 
                   "transformation" + 
                     match s with
                     None -> ""
                     | Some key -> "/" + key
               | Source -> "source"
    type CalculatorService =
        Calculate
        with member x.ToPath() = 
                match x with
                Calculate -> "/data/calculate"
    type CacheService = 
        Read of string
        | Update
        with member x.ToPath() =
              match x with
              Read key -> "/read/" + key
              | Update -> "/update"
    type Service = 
         UniformData of CacheService
         | Calculator of CalculatorService
         | Configurations of ConfigurationService
         | Collector of string * CollectorService
         with 
             member x.ToStrings() = 
               match x with
               UniformData serv -> "uniformdata", serv.ToPath()
               | Calculator serv -> "calculator",serv.ToPath()
               | Configurations serv -> "configurations", serv.ToPath()
               | Collector (collectorName,service) ->  
                   collectorName.ToLower().Replace(" ","") 
                   |> sprintf "%scollector", service.ToPath()
             member x.ServiceUrl 
                  with get() = 
                      x.ToStrings()
                      ||> sprintf "http://%s-svc:8085/%s" 


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
    
    let get (service : Service) parser  = 
        let url = service.ServiceUrl
        Log.logf "Getting %s" url
        Http.Request(url,
                     httpMethod = "GET"
        ) |> readResponse parser

    let private putOrPost parser httpMethod (service : Service) body = 
        let url = service.ServiceUrl
        Log.logf "%sting to %s" httpMethod url
        Http.Request(url,
                     httpMethod = httpMethod,
                     body = TextRequest body
        ) |> readResponse parser

    let put = putOrPost id "PUT"
    let post service parser body = putOrPost parser "POST" service body