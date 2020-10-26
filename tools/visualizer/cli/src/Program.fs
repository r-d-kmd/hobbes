open Hobbes
open System.IO
open FSharp.Data.JsonExtensions
open FSharp.Formatting.Markdown
open Hobbes.FSharp.DataStructures

type Value = Parsing.AST.Value
let md5Hash (input : string) =
    use md5Hash = System.Security.Cryptography.MD5.Create()
    let data = md5Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input))
    let sBuilder = System.Text.StringBuilder()
    (data
    |> Seq.fold(fun (sBuilder : System.Text.StringBuilder) d ->
            sBuilder.Append(d.ToString("x2"))
    ) sBuilder).ToString()  

let readValue (json:FSharp.Data.JsonValue)  = 
    let rec inner json namePrefix = 
        let wrap (v : #System.IComparable) = 
          [|System.String.Join(".", namePrefix |> List.rev),v :> System.IComparable|]
        match json with
        | FSharp.Data.JsonValue.String s ->  
            match System.Double.TryParse(s,System.Globalization.NumberStyles.Any,System.Globalization.NumberFormatInfo.InvariantInfo) with
            true,f -> f |> wrap
            | _ -> 
                match System.DateTime.TryParse(s) with
                true,dt -> dt |> wrap
                | _ -> 
                    match System.Int32.TryParse(s) with
                    true,n -> n |> wrap
                    | _ -> s |> wrap
        | FSharp.Data.JsonValue.Number d -> d |> wrap
        | FSharp.Data.JsonValue.Float f -> f |> wrap
        | FSharp.Data.JsonValue.Boolean b ->  b |> wrap
        | FSharp.Data.JsonValue.Record properties ->
            properties
            |> Array.collect(fun (name,v) ->
                name::namePrefix |> inner v 
            )
        | FSharp.Data.JsonValue.Array elements ->
             elements
             |> Array.indexed
             |> Array.collect(fun (i,v) ->
                 (string i)::namePrefix |> inner v 
             ) 
        | FSharp.Data.JsonValue.Null -> wrap ""
    inner json []
    
type CachedData = FSharp.Data.JsonProvider<"""{"columnNames":["Todo","Doing","Done","SprintNumber"],"values":[["string","string",null,null],[{},{},{},{}],[48.4,42.3,92.0,53.0],[57.0,51.0,64.0,54.0],[50.0,69.0,2.0,55.0],[26.0,30.0,null,56.0]],"rowCount":6}""">
//switch for chart type and transformation file
[<EntryPoint>]
let main args =
    let cacheDirectory = ".cache"
    if Directory.Exists cacheDirectory |> not then Directory.CreateDirectory cacheDirectory |> ignore
    
    let defaultFile() =
        match Directory.EnumerateFiles(".","*.hb") |> Seq.tryHead with
        None -> failwith "Either a file path must be provided as an argument or there must be at least one .hb file in the working directory"
        | Some file -> file 
    let arguments = 
        match args with
        [||] -> 
            [
                "file", defaultFile() 
                "chart", "column"
            ] |> Map.ofList
        | [|chart|] when not (chart.ToLower().EndsWith ".hb") -> 
            [
                "file", defaultFile()
                "chart", chart.ToLower()
            ] |> Map.ofList
        | [|file|] -> 
            [
                "file", file
                "chart", "column"
            ] |> Map.ofList
        | [|file;chart|] ->
            [
                "file", file
                "chart", chart.ToLower()
            ] |> Map.ofList 
        | args ->
            let file = args.[0]
            let chart = args.[1].ToLower()
            let properties = 
                args
                |> Array.skip 2
                |> Array.map(fun arg ->
                    match arg.Split '=' with
                    [|key|] -> key,""
                    | [|key;value|] -> key,value
                    | args -> args.[0],args.[1]
                ) |> Map.ofArray
            properties.Add("file",file).Add("chart",chart)
    let chartType = 
        match arguments.["chart"] with
        "scatter" -> Charting.Scatter
        | "line" -> Charting.Line
        | "column" -> Charting.Column
        | "area" -> Charting.Area
        | "chandlestick" -> Charting.Candlestick
        | "pie" -> Charting.Pie
        | "bubble" -> Charting.Bubble
        | "gauge" -> Charting.Gauge
        | "calendar" -> Charting.Calendar
        | "geo" -> Charting.Geo
        | "table" -> Charting.Table
        | c ->
            eprintfn "Unknown chart type %s using column" c
            Charting.Column     
    let file = arguments.["file"]
    let arguments = 
        match arguments |> Map.tryFind "title" with
        None -> arguments.Add("title",Path.GetFileNameWithoutExtension (arguments.["file"]) )
        | Some _ -> arguments

    let arguments = 
        match arguments |> Map.tryFind "no-cache" with
        None -> arguments.Add("no-cache", "false")
        | Some _ -> arguments
            
    let noCache = arguments.["no-cache"].ToLower() = "true"
    
    let contents = File.ReadAllText(file)
    
    printfn "Loaded '%s' of length %d" file contents.Length
    
    let chunks = 
        try
            FSharp.Compile.compile contents
        with e ->
            eprintfn "Failed to compile transformation %s. Message: %s" contents e.Message
            reraise()
    let chunk = chunks |> List.exactlyOne
    let source = chunk.Source
    
    let getStringFromValue name = 
        match source.Properties |> Map.tryFind name with
        Some (Value.String s) -> s |> Some
        | Some(Value.Null) -> None
        | None -> 
            arguments |> Map.tryFind name
        | _ -> failwithf "%s must be a string" name

    let requestData f =
        let valueProp = getStringFromValue "values"
        let urls = 
                match source.Properties |> Map.tryFind "url" with
                Some(Value.String url) -> [url]
                | Some(Value.Sequence elements) ->
                    elements
                    |> List.map(function
                        Value.String s -> s 
                        | Value.Null -> null
                        | Value.Decimal d -> string d
                        | Value.Boolean b -> string b
                        | v -> failwithf "%A must be a string" v
                    )
                | url -> failwithf "Url must either be a string or a sequence but was %A" url
        urls
        |> Seq.map(fun url -> 
            let doc = 
                
                let cacheFile = cacheDirectory + "/" + (url |> md5Hash) + ".json"
                if noCache || File.Exists cacheFile |> not then
                    let data = f url
                    if not noCache then
                       File.WriteAllText(cacheFile,data)
                    data
                else
                    File.ReadAllText cacheFile
                |> FSharp.Data.JsonValue.Parse
            
            match valueProp with
            None -> 
                match doc with
                FSharp.Data.JsonValue.Array a -> a
                | _ -> failwith "The root of the returned JSON doc must be an array or a name of the value property must be specified in the configuration"
            | Some v ->  
                let arr = 
                    doc.Properties 
                    |> Array.find(fun (name,_) -> name = v) 
                    |> snd
                match arr with
                FSharp.Data.JsonValue.Array a -> a
                | _ -> failwith "The value property specified with the setting `values` must be an array"
        ) |> Seq.collect(
            Seq.collect readValue
        ) |> Seq.groupBy fst
        |> Seq.map(fun (columnName,cells) -> 
            columnName,cells 
                       |> Seq.mapi(fun i (_,value) ->
                           Parsing.AST.KeyType.Create i,value
                       )
        )
           
    let data =
        match source.ProviderName.ToLower() with
        "odata" -> ODataProvider.read source
        | "rest" ->
            let user = getStringFromValue "user"
            let pwd = getStringFromValue "pwd"
            let method = getStringFromValue "method"
            
            requestData (fun url ->
                printfn "Reading data from %s" url
                FSharp.Data.Http.RequestString(url,
                    headers = [
                      if user.IsSome then yield FSharp.Data.HttpRequestHeaders.BasicAuth user.Value pwd.Value
                    ],
                    httpMethod =
                        match method with
                        None -> "GET"
                        | Some m -> m
                ) 
            )

        | provider ->
           failwithf "Didn't recognise provider: %s" provider

    let transformation = 
        chunk.Blocks
        |> List.filter(function
           | FSharp.Compile.Transformation _ -> true
           | _ -> false
        ) |> List.fold(fun f' t ->
            match t with
            FSharp.Compile.Transformation f ->
                f' >> f
            | _ -> f'
        ) id

    let renderedBlocks = 
        chunk.Blocks
        |> List.map(function
            FSharp.Compile.Transformation _ -> 
                let dataCacheFile = cacheDirectory + "/" + file
                let renderedChart = 
                    let transformedData = 
                        let res = 
                            data
                            |> DataMatrix.fromTable
                            |> transformation 
                        File.WriteAllText(dataCacheFile,res.ToJson())
                        printfn "Transformatin complete"
                        res :?> DataMatrix
                        
                    Charting.render transformedData chartType arguments.["title"]
                renderedChart.GetInlineHtml()
            | FSharp.Compile.Comment cmt ->
                let parsed = 
                    cmt
                    |> Markdown.Parse
                Markdown.ToHtml(parsed,System.Environment.NewLine)
        )

    let htmlTemplate = 
        sprintf """<!DOCTYPE html>
<html>
    <head>
        <meta charset="UTF-8">
        <meta http-equiv="X-UA-Compatible" content="IE=edge" />
        <title>%s</title>
        <script type="text/javascript" src="https://www.gstatic.com/charts/loader.js"></script>
        <script src="https://cdn.plot.ly/plotly-latest.min.js"></script>
    </head>
    <body>
        %s
    </body>
</html>""" (arguments.["title"])
    let filename = arguments.["title"] + ".html"
    System.IO.File.WriteAllText("html/" + filename, System.String.Concat renderedBlocks |> htmlTemplate )
    0