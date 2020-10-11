namespace Shared

open System

type ChartType =
    Line
    | Column
    | Scatter

type YValue =
    Text of string
    | Date of DateTime
    | Number of float

type Point =
    {
        Y : YValue
        X : float
    }

type ChartModel =
    { Id : string
      ChartType : ChartType
      Data : Point [] []
      }

module Route =
    let builder typeName methodName =
        sprintf "/api/%s/%s" typeName methodName

type IChartApi =
    {
        getCharts : string -> Async<ChartModel list>
    }