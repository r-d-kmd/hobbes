(*** hide ***)
#r "../../packages/visualizer/Newtonsoft.Json/lib/netstandard2.0/Newtonsoft.Json.dll"
#r "../../packages/visualizer/XPlot.Plotly/lib/netstandard2.0/XPlot.Plotly.dll"
#r "../../packages/visualizer/Deedle/lib/netstandard2.0/Deedle.dll"
#r "../../packages/visualizer/MathNet.Numerics/lib/netstandard2.0/MathNet.Numerics.dll"
#r "../../packages/visualizer/MathNet.Numerics.FSharp/lib/netstandard2.0/MathNet.Numerics.FSharp.dll"
#r "../../packages/visualizer/Accord.MachineLearning/lib/netstandard2.0/Accord.MachineLearning.dll"
#r "../../packages/visualizer/FParsec/lib/netstandard2.0/FParsec.dll"
#r "../../packages/visualizer/FParsec/lib/netstandard2.0/FParsecCS.dll"
#r "../../packages/visualizer/FSharp.Data/lib/netstandard2.0/FSharp.Data.dll"
#r "../../packages/visualizer/XPlot.GoogleCharts/lib/netstandard2.0/XPlot.GoogleCharts.dll"
#r "../../common/hobbes.core/src/bin/Debug/netcoreapp3.1/hobbes.core.dll"

open Hobbes
open System.IO
open FSharp.Data.JsonExtensions

let file = __SOURCE_DIRECTORY__ + "/transformation.hb"
let contents = File.ReadAllText(file)
printfn "Loaded '%s' of length %d" file contents.Length

let chunks = Hobbes.FSharp.Compile.compile contents
let chunk = chunks |> List.exactlyOne
printfn "Blocks: %A" chunk.Blocks
let transformation = 
    chunk.Blocks 
    |> List.filter(function
       | Hobbes.FSharp.Compile.Transformation _ -> true
       | _ -> false
    ) |> List.fold(fun f' t ->
        match t with
        Hobbes.FSharp.Compile.Transformation f ->
            f' >> f
        | _ -> f'
    ) id

let source = chunk.Source
type Value = Parsing.AST.Value
let getStringFromValue name = 
    match source.Properties |> Map.tryFind name with
    Some (Value.String s) -> s |> Some
    | Some(Value.Null)
    | None -> None
    | _ -> failwithf "%s must be a string" name

let data =
    if source.ProviderName.ToLower() = "rest" then
        let url = (getStringFromValue "url").Value
        let user = getStringFromValue "user"
        let pwd = getStringFromValue "pwd"
        let method = getStringFromValue "method"
        let valueProp = getStringFromValue "values"
        let doc = 
            FSharp.Data.Http.RequestString(url,
                headers = [
                  if user.IsSome then yield FSharp.Data.HttpRequestHeaders.BasicAuth user.Value pwd.Value
                ],
                httpMethod =
                    match method with
                    None -> "GET"
                    | Some m -> m
            ) |> FSharp.Data.JsonValue.Parse

        let readValue (json:FSharp.Data.JsonValue) = 
            let rec inner json namePrefix = 
                let wrap v = 
                  [|System.String.Join(".", namePrefix |> List.rev),v|]
                match json with
                | FSharp.Data.JsonValue.String s ->  s |> string |> wrap
                | FSharp.Data.JsonValue.Number d -> d |> string |> wrap
                | FSharp.Data.JsonValue.Float f -> f |> string |> wrap
                | FSharp.Data.JsonValue.Boolean b ->  b |> string |> wrap
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
        let values = 
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
        values
        |> Seq.collect readValue
        |> Seq.groupBy fst
        |> Seq.map(fun (columnName,cells) -> 
            columnName,cells 
                       |> Seq.mapi(fun i (_,value) ->
                           Hobbes.Parsing.AST.KeyType.Create i,value
                       )
        )
    else
       Seq.empty
printfn "DATA: %A" data
let table =  
    let transformedData = 
        data
        |> Hobbes.FSharp.DataStructures.DataMatrix.fromTable
        |> transformation 
        :?> Hobbes.FSharp.DataStructures.DataMatrix

    transformedData.AsTable()    
    |> Seq.map(fun (n,ser) ->
        n,
        ser
        |> Seq.map(fun (k,v) ->
            k |> Parsing.AST.KeyType.UnWrap
            :?> System.IConvertible,v :> obj :?> System.IConvertible
        )
    ) 

let columnNames = table |> Seq.map fst

let scatterChart =    
    table
    |> Seq.map snd
    |> XPlot.Plotly.Chart.Scatter

let lineChart =    
    table
    |> Seq.map snd
    |> XPlot.Plotly.Chart.Line
    
let columnChart =    
    table
    |> Seq.map snd
    |> XPlot.Plotly.Chart.Column

let pieChart = 
    table
    |> Seq.map snd
    |> Seq.head
    |> XPlot.Plotly.Chart.Pie

let bubbleChart = 
    let data = 
        table
        |> Seq.map snd
    (data |> Seq.head)
    |> Seq.zip (data |> Seq.tail |> Seq.head)
    |> Seq.map (fun ((a,b),(c,_)) -> a,b,c)
    |> XPlot.Plotly.Chart.Bubble

let chart = 
    //scatterChart
    bubbleChart
    |> XPlot.Plotly.Chart.WithLegend true
    |> XPlot.Plotly.Chart.WithLabels columnNames

System.IO.File.WriteAllText(__SOURCE_DIRECTORY__ + "/chart.html",chart.GetHtml())