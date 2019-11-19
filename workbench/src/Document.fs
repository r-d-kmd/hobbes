namespace Wotkbench

open Hobbes.Server.Reflection

module Document =
    let areaTypes = getTypesWithAttribute<Routing.RouteAreaAttribute>
    let documents = 
        areaTypes
        |> Seq.map(fun areaType ->
            let areaAtt = areatType |> getAttribute<Routing.RouteAreaAttribute>
            let areaName = areaAtt.Path
            let shouldAuthenticate = areaAtt.ShouldAuthenticate
            let routes =
                areaType
                |> getMethodsWithAttribute<RouteHandlerAttribute>
                |> Seq.map(fun (handlerAtt,method) ->
                    let subpath = handlerAtt.Path
                    let body = 
                        if handlerAtt.HasBody then 
                            handlerAtt.Body
                        else
                            ""
                    let result = handlerAtt.Result
                    let verb = handlerAtt.Verb
                    let p::arts =
                        subpath.Replace("%%","..").Split('%', System.StringSplitOptions.RemoveEmptyEntries)
                        |> List.ofArray

                    let arguments =
                        (method.GetParameters()
                         |> Array.map(fun p -> ":" + p.Name)
                         |> List.ofArray)@[""]

                    let path = 
                        areaName +
                            //remove the character following % in the path
                            p::(arts |> List.map(fun s -> s.Substring(1)))
                            |> List.zip arguments
                            |> List.map(fun (parameterName,pathPart) ->
                                pathPart + parameterName
                            ) |> String.concat
                    
                    
                )
        )