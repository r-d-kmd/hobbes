module Charting

open Hobbes
open Hobbes.FSharp.DataStructures

type ChartType = 
    Scatter
    | Line
    | Column
    | Area
    | Candlestick
    | Pie
    | Bubble
    | Gauge
    | Calendar
    | Geo
    | Table
type Chart = 
    Google of XPlot.GoogleCharts.GoogleChart
    | Plotly of XPlot.Plotly.PlotlyChart
    with member x.GetInlineHtml() = 
            match x with
            Google chart -> chart.GetInlineHtml()
            | Plotly chart -> chart.GetInlineHtml()
         static member WithLegend show =
             function
             Google c -> c
                         |> XPlot.GoogleCharts.Chart.WithLegend show |> Google
             | Plotly c -> c
                         |> XPlot.Plotly.Chart.WithLegend show |> Plotly
         static member WithLabels show =
             function
             Google c -> c
                         |> XPlot.GoogleCharts.Chart.WithLabels show |> Google
             | Plotly c -> c
                         |> XPlot.Plotly.Chart.WithLabels show |> Plotly
         static member WithTitle show =
             function
             Google c -> c
                         |> XPlot.GoogleCharts.Chart.WithTitle show |>  Google
             | Plotly c -> c
                         |> XPlot.Plotly.Chart.WithTitle show |>  Plotly
         static member Scatter (table : seq<string*seq<System.IConvertible*System.IConvertible>>) =
             let series = 
                table
                |> Seq.map snd  
             XPlot.Plotly.Chart.Scatter series |> Plotly
         static member Line (table : seq<string*seq<System.IConvertible*System.IConvertible>>) =
             let series = 
                table
                |> Seq.map snd  
             XPlot.Plotly.Chart.Line series |> Plotly
         static member Area (table : seq<string*seq<System.IConvertible*System.IConvertible>>) =
             let series = 
                table
                |> Seq.map snd  
             XPlot.Plotly.Chart.Area series |> Plotly
         static member Bar (table : seq<string*seq<System.IConvertible*System.IConvertible>>) =
             let series = 
                table
                |> Seq.map snd  
             XPlot.Plotly.Chart.Bar series |> Plotly
         static member Column (table : seq<string*seq<System.IConvertible*System.IConvertible>>) =
             let series = 
                table
                |> Seq.map snd   
             XPlot.Plotly.Chart.Column series |> Plotly
         static member Candlestick (table : seq<string*seq<System.IConvertible*System.IConvertible>>) =
            let series = 
                table
                |> Seq.take 5
                |> Seq.map (snd >> (Seq.map snd) >> Array.ofSeq)
                |> Array.ofSeq
            let series = 
                [for i in 0..series.[0].Length - 1 ->                    
                    series.[0].[i],series.[1].[i],series.[2].[i],series.[3].[i],series.[4].[i]
                ]
            XPlot.Plotly.Chart.Candlestick series
            |> Plotly
         static member Gauge (table : seq<string*seq<System.IConvertible*System.IConvertible>>) =
            let options =
                XPlot.GoogleCharts.Configuration.Options(
                    width = 400,
                    height = 120,
                    redFrom = 90,
                    redTo = 100,
                    yellowFrom = 75,
                    yellowTo = 90,
                    minorTicks = 5
                )
            let series = 
                table
                |> Seq.filter(fun (_,v) -> v |> Seq.isEmpty |> not)
                |> Seq.map(fun (columnName,values) ->
                    columnName, values
                                |> Seq.averageBy(fun (_,v) -> 
                                    match v with
                                    :? decimal as v -> float v
                                    | :? int as v -> float v
                                    | :? float as v -> v
                                    | :? string as v -> float v
                                    | _ -> failwithf "Can't convert %A to float" v
                                ) |> int
                )
            assert(series |> Seq.isEmpty |> not)
            XPlot.GoogleCharts.Chart.Gauge series
            |> XPlot.GoogleCharts.Chart.WithOptions options
            |> Google
         static member Pie =
             (Seq.map snd)
             >> Seq.head
             >> XPlot.Plotly.Chart.Pie
             >> Plotly
         static member Bubble (table : seq<string*seq<System.IConvertible*System.IConvertible>>) =
            let series = 
                table
                |> Seq.map snd  
            (series |> Seq.head)
            |> Seq.zip (series |> Seq.tail |> Seq.head)
            |> Seq.map (fun ((a,b),(c,_)) -> a,b,c)
            |> XPlot.Plotly.Chart.Bubble
            |> Plotly
         static member Calendar (table : seq<string*seq<System.IConvertible*System.IConvertible>>) =
            let series =
                table
                |> Seq.take 2
                |> Seq.map (snd >> (Seq.map snd))
            let dates = 
                series 
                |> Seq.head
                |> Seq.map(function
                            :? System.DateTime as dt -> dt
                            | :? int as ticks -> System.DateTime(int64 ticks)
                            | :? float as ticks -> System.DateTime(int64 ticks)
                            | :? decimal as ticks -> System.DateTime(int64 ticks)
                            | :? int64 as ticks -> System.DateTime(ticks)
                            | :? string as dateString -> System.DateTime.Parse dateString
                            | v -> failwithf "Can't convert %A to datetime" v
                )
            let values = 
                series
                |> Seq.tail
                |> Seq.head
            let series = 
                values
                |> Seq.zip dates
            XPlot.GoogleCharts.Chart.Calendar series |> Google
         static member Geo (table : seq<string*seq<System.IConvertible*System.IConvertible>>) =
            let series =
                table
                |> Seq.take 2
                |> Seq.map (snd >> (Seq.map snd))
            let countries = 
               series
               |> Seq.head
               |> Seq.map string
            let values = 
                series
                |> Seq.tail
                |> Seq.head
                |> Seq.map(function
                            | :? int as v -> float v
                            | :? float as v -> float v
                            | :? decimal as v -> float v
                            | :? int64 as v -> float v
                            | :? string as v -> float v
                            | v -> failwithf "Can't convert %A to float" v
                )
            let series = 
                values
                |> Seq.zip countries
            XPlot.GoogleCharts.Chart.Geo series |> Google
         static member Table (table : seq<string*seq<System.IConvertible*System.IConvertible>>) =
            let series = 
                table
                |> Seq.map snd 
            XPlot.GoogleCharts.Chart.Table series |> Google


let render (transformedData : DataMatrix) chartType title =
    let table =  
        transformedData.AsTable()    
        |> Seq.map(fun (n,ser) ->
            n,
            ser
            |> Seq.map(fun (k,v) ->
                match k with
                  Parsing.AST.KeyType.List k -> 
                    System.String.Join(".",
                      k |> List.map Parsing.AST.KeyType.UnWrap
                    ) :> System.IConvertible
                  | k -> k |> Parsing.AST.KeyType.UnWrap :?> System.IConvertible
                ,v :> obj :?> System.IConvertible
            )
        ) 

    printfn "%A" table
    
    let columnNames = table |> Seq.map fst

    let charter =    
        match chartType with
        Scatter -> Chart.Scatter >> Chart.WithLabels columnNames
        | Line -> Chart.Line >> Chart.WithLabels columnNames
        | Column -> Chart.Column >> Chart.WithLabels columnNames
        | Area -> Chart.Area >> Chart.WithLabels columnNames
        | Candlestick -> Chart.Candlestick >> Chart.WithLabels columnNames
        | Gauge -> Chart.Gauge 
        | Pie -> Chart.Pie >> Chart.WithLabels columnNames
        | Bubble -> Chart.Bubble  >> Chart.WithLabels columnNames
        | Calendar -> Chart.Calendar
        | Geo -> Chart.Geo
        | Table -> Chart.Table

    let renderedChart = 
        table
        |> charter
        |> Chart.WithLegend true
        |> Chart.WithTitle title
    renderedChart