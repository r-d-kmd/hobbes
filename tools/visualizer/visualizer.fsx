(*** hide ***)
#r "../../packages/visualizer/Newtonsoft.Json/lib/netstandard2.0/Newtonsoft.Json.dll"
#r "../../packages/visualizer/XPlot.Plotly/lib/netstandard2.0/XPlot.Plotly.dll"
#r "../../packages/visualizer/Deedle/lib/netstandard2.0/Deedle.dll"
#r "../../packages/visualizer/MathNet.Numerics/lib/netstandard2.0/MathNet.Numerics.dll"
#r "../../packages/visualizer/MathNet.Numerics.FSharp/lib/netstandard2.0/MathNet.Numerics.FSharp.dll"
#r "../../packages/visualizer/Accord.MachineLearning/lib/netstandard2.0/Accord.MachineLearning.dll"
#r "../../packages/visualizer/FParsec/lib/netstandard2.0/FParsec.dll"
#r "../../packages/visualizer/FParsec/lib/netstandard2.0/FParsecCS.dll"
#r "../../packages/visualizer/XPlot.GoogleCharts/lib/netstandard2.0/XPlot.GoogleCharts.dll"
#r "../../common/hobbes.core/src/bin/Debug/netcoreapp3.1/hobbes.core.dll"

open Hobbes
open System.IO

let file = __SOURCE_DIRECTORY__ + "/transformation.hb"
let contents = File.ReadAllLines(file)
printfn "Loaded '%s' of length %d" file contents.Length

let transformationf = Hobbes.FSharp.Compile.expressions contents

let data = 
    [
        "Sprint no",[
            1
            2
            3
            4
            5
            6
            7
            8
            9
        ]
        "FTEs",[
            1
            4
            2
            3
            1
            3
            3
            3
            2
        ]
        "Completed PBIs", [
            0
            3
            5
            12
            6
            8
            1
            2
            2
        ]
    ]
    |> List.map(fun (n,l) -> 
        n,  l 
            |> List.mapi(fun i v -> 
                Parsing.AST.KeyType.Create i,v
            )
    )

let table =  
    let transformedData = 
        data
        |> Hobbes.FSharp.DataStructures.DataMatrix.fromTable
        |> transformationf 
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