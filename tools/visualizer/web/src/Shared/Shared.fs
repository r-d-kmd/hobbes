namespace Shared

open System

type ChartType =
    Line
    | Column
    | Scatter

type PointValue =
    Text of string
    | Date of DateTime
    | Number of float

type Point =
    {
        Y : PointValue
        X : PointValue
    }

type ChartModel =
    {
        Id : string
        Title : string
        ChartType : ChartType
        Data : seq<seq<Point>>
    }

type AreaModel =
    {
        Id : string
        ChartIds : string list
    }

module Route =
    let builder typeName methodName =
        sprintf "/api/%s/%s" typeName methodName

type IChartApi =
    {
        getChart : string -> Async<ChartModel>
        getAreas : unit -> Async<AreaModel list>
    }