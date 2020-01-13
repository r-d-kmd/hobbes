namespace Hobbes.Server

open FSharp.Data

module Request =

    let url = "http://azuredevopscollector-svc:8085/"

    let readBody = function
    | Binary b -> System.Text.Encoding.ASCII.GetString b
    | Text t -> t

    let request method path =
        let response = Http.Request(url+path, httpMethod = method, silentHttpErrors = true, customizeHttpRequest = (fun request -> request.Timeout <- System.Int32.MaxValue ; request))
        response.StatusCode, (readBody response.Body)

    let get path =
        request HttpMethod.Get path

    let delete path =
        request HttpMethod.Delete path    
