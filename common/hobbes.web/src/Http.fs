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
                "data" ::
                match x with
                Read -> ["read"]
                | Sync -> ["sync"]
    type ConfigurationService =
        Configuration of string option
        | Transformation of string option
        | DependingTransformations of string
        | Collectors
        | Sources of string
        | Source
        with member x.ToPath() =
               match x with
               Configuration s -> 
                   "configuration" ::
                     match s with
                     None -> []
                     | Some key -> [key]
               | Transformation s -> 
                   "transformation" ::
                     match s with
                     None -> []
                     | Some key -> [key]
               | Source -> ["source"]
               | Collectors -> ["collectors"]
               | Sources collector ->
                   ["sources";collector]
               | DependingTransformations cacheKey ->
                   ["dependingtransformations"; cacheKey]
    type CalculatorService =
        Calculate of string
        with member x.ToPath() = 
                match x with
                Calculate key -> 
                    [
                        "calculate"
                        key
                    ]
    type CacheService = 
        Read of string
        | Update
        with member x.ToPath() =
              match x with
              Read key -> 
                  [
                      "read"
                      key
                  ]
              | Update -> ["update"]

    type DbService =
       Root
       | Database of string
       with member x.ToPath() = 
              match x with
              Root -> []
              | Database s -> [s]
           
    type Service = 
         UniformData of CacheService
         | Db of DbService
         | Calculator of CalculatorService
         | Configurations of ConfigurationService
         | Collector of string * CollectorService
         with 
             member x.ToParts() = 
               match x with
               UniformData serv -> "uniformdata", serv.ToPath(),8085
               | Calculator serv -> "calculator",serv.ToPath(),8085
               | Configurations serv -> "configurations", serv.ToPath(),8085
               | Db serv -> "db",serv.ToPath(),5984
               | Collector (collectorName,service) ->  
                   collectorName.ToLower().Replace(" ","") 
                   |> sprintf "collectors-%s", service.ToPath(),8085
             member x.ServiceUrl 
                  with get() = 
                      let serviceName,path,port = x.ToParts()
                      let pathString = System.String.Join("/",path) 
                      sprintf "http://%s-svc:%d/%s"  serviceName port pathString


    let readBody = 
        function
            | Binary b -> System.Text.Encoding.Unicode.GetString b
            | Text t -> 
                try
                    t
                    |> System.Convert.FromBase64String
                    |> System.Text.Encoding.Unicode.GetString
                with e ->
                   printfn "Couldn't read %s as base64. %s" t e.Message
                   t
            
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
        printfn "Getting %s" url
        Http.Request(url,
                     httpMethod = "GET",
                     silentHttpErrors = true
        ) |> readResponse parser

    let private putOrPost parser httpMethod (service : Service) (body : string) = 
        let url = service.ServiceUrl
        printfn "%sting binary to %s" httpMethod url
        Http.Request(url,
                     httpMethod = httpMethod,
                     body = 
                         (body
                          |> System.Text.Encoding.Unicode.GetBytes
                          |> System.Convert.ToBase64String
                          |> TextRequest),
                     silentHttpErrors = true
        ) |> readResponse parser

    let put = putOrPost id "PUT"
    let post service parser body = putOrPost parser "POST" service body