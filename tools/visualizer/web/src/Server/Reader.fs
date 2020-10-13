module Reader
open Hobbes
open System.IO
open FSharp.Data.JsonExtensions
open Hobbes.FSharp.Compile
open Hobbes.FSharp.DataStructures
open Shared
open Hobbes.Parsing

type Value = Parsing.AST.Value

let private md5Hash (input : string) =
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

let private cacheDirectory =
    let name = ".cache"
    if Directory.Exists name |> not then Directory.CreateDirectory name |> ignore
    name
let private areas =
    System.IO.Directory.EnumerateDirectories "transformations"
    |> Seq.map(fun d ->
        let areaId = System.IO.Path.GetFileNameWithoutExtension d
        {
                Id = areaId
                ChartIds =
                    System.IO.Directory.EnumerateFiles(d,"*.hb")
                    |> Seq.map(fun f ->
                        System.IO.Path.GetFileNameWithoutExtension f
                    ) |> List.ofSeq
        }
    )

let read chartId =

    async {
        let filename = chartId + ".hb"
        let title = System.IO.Path.GetFileNameWithoutExtension filename
        let path = System.IO.Path.Combine("transformations", filename)
        printfn "Reading %s" path
        let! contents = path |> System.IO.File.ReadAllTextAsync  |> Async.AwaitTask
        let chunks =
            try
                compile contents
            with e ->
                printfn "Failed to compile transformation %s. Message: %s" contents e.Message
                reraise()

        let chunk = chunks |> List.exactlyOne
        let source = chunk.Source
        let sourceProperties = source.Properties
        let transformation =
            chunk.Blocks
            |> List.fold(fun f' t ->
                match t with
                Transformation f ->
                    f' >> f
                | _ -> f'
            ) id

        let getStringFromValue name =
            match sourceProperties |> Map.tryFind name with
            Some (Value.String s) -> s |> Some
            | Some(Value.Null) -> None
            | Some v -> failwithf "%s must be a string. %A" name v
            | None -> None
            | _ -> failwithf "Couldn't find a property named %s" name

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
                    if File.Exists cacheFile |> not then
                        printfn "Cache file (%s) doesn't exist" cacheFile
                        let data = f url
                        printfn "Writing data to file %s" cacheFile
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
        let dataCacheFile = System.IO.Path.Combine(cacheDirectory,chartId + ".data.json")
        let toJson =
            function
            Number f -> f |> string
            | Text t -> sprintf "%A" t
            | Date dt -> sprintf "%A" (dt.ToString())
        let data =
            if File.Exists dataCacheFile |> not then
                let series =
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


                let data =
                    (series
                     |> Hobbes.FSharp.DataStructures.DataMatrix.fromTable
                     |> transformation
                     :?> FSharp.DataStructures.DataMatrix).AsTable()
                    |> Seq.map(fun (_,values) ->
                        values
                        |> Seq.map(fun ((key : AST.KeyType),(value : System.IComparable)) ->
                            let x =
                                match key with
                                AST.KeyType.Numbers n -> Number n
                                | AST.KeyType.Text t -> Text t
                                | AST.KeyType.DateTime dt -> Date dt
                                | AST.KeyType.Obj o -> o |> string |> Text
                                | AST.KeyType.List l -> System.String.Join("-",l) |> Text
                                | AST.KeyType.Missing -> failwith "A key can't be missing"
                            let y =
                                match value with
                                :? int as i -> i |> float |> Number
                                | :? int64 as i -> i |> float |> Number
                                | :? decimal as i -> i |> float |> Number
                                | :? float as i -> i |> Number
                                | :? string as t -> Text t
                                | :? System.DateTime as dt -> Date dt
                                | :? System.DateTimeOffset as dt -> dt.DateTime |> Date
                                | o -> o |> string |> Text
                            {
                                X = x
                                Y = y
                            }
                        )
                    )
                let json =
                    System.String.Join(",",data
                        |> Seq.map(fun series ->
                            System.String.Join(",",
                                series
                                |> Seq.map(fun point ->
                                    let x = point.X |> toJson
                                    let y = point.Y |> toJson
                                    sprintf """{"x":%s,"y":%s}""" x y
                                )) |> sprintf "[%s]"
                        )) |> sprintf "[%s]"
                let dir = Path.GetDirectoryName dataCacheFile
                if Directory.Exists dir |> not then Directory.CreateDirectory dir |> ignore
                File.WriteAllText(dataCacheFile,json)
                data
            else
                printfn "Using cached data file for %s" chartId
                let toPoint (v : System.IComparable) =
                    match v with
                    :? int as i -> i |> float |> Number
                    | :? float as i -> Number i
                    | :? decimal as i -> i |> float |> Number
                    | :? string as i -> Text i
                    | :? System.DateTime as dt -> Date dt
                    | v -> v |> string |> Text

                match File.ReadAllText dataCacheFile
                      |> FSharp.Data.JsonValue.Parse with
                | FSharp.Data.JsonValue.Array elements ->
                    elements
                    |> Seq.map(function
                         | FSharp.Data.JsonValue.Array series ->
                            series
                            |> Seq.map(fun e ->
                                let props =
                                    e.Properties
                                    |> Map.ofArray
                                let x =
                                    match props.["x"] |> readValue with
                                    [|_,x|] -> x
                                    | _ -> failwith "Only expected one value for x"
                                let y =
                                    match props.["y"] |> readValue with
                                    [|_,y|] -> y
                                    | _ -> failwith "Only expected one value for y"
                                {
                                    X = x |> toPoint
                                    Y = y |> toPoint
                                }
                            )
                         | _ -> failwith "Expected an array"
                    )
                | _ -> failwith "Expected an array of arrays"

        return {
            Id = chartId
            Title = title
            Data = data
            ChartType = Column
        }
    }

let cache() =
    areas
    |> Seq.collect(fun area ->
        area.ChartIds
        |> Seq.map(fun chartId ->
            let createChartId = sprintf "%s/%s" area.Id
            let chartId = chartId |> createChartId
            printfn "Caching %s" chartId
            async{
                let! _ = chartId |> read
                ()
            }
        )
    ) |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore