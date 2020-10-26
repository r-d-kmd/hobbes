module Index

open Elmish
open Fable.Remoting.Client
open Shared
open Fable.Core
open Fable.Recharts
open Fable.Recharts.Props
module R = Fable.React.Standard
module P = Fable.React.Props
open Fable.FontAwesome

let margin t r b l =
    Chart.Margin { top = t; bottom = b; right = r; left = l }


type Model =
    { Areas: AreaModel list
      SelectedChart : ChartModel
    }

type Msg =
    | GotAreas of AreaModel list
    | ChartSelected of string
    | GotChart of ChartModel
let ChartApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<IChartApi>

let init(): Model * Cmd<Msg> =
    let model =
        { Areas = []
          SelectedChart = {
              Id = ""
              Title = ""
              Data = [||]
              ChartType = Column
          }}
    let cmd = Cmd.OfAsync.perform ChartApi.getAreas () GotAreas
    model, cmd

let update (msg: Msg) (model: Model): Model * Cmd<Msg> =
    match msg with
    | GotAreas areas ->
        let headArea = areas |> List.head
        let chartId = sprintf "%s/%s" headArea.Id (headArea.ChartIds |> List.head)
        { model with Areas = areas }, chartId |> ChartSelected |> Cmd.ofMsg
    | ChartSelected chartId ->
        { model with
            SelectedChart = {
              Id = ""
              Title = ""
              Data = [||]
              ChartType = Column
            }
        }, Cmd.OfAsync.perform ChartApi.getChart chartId GotChart
    | GotChart chart ->
        { model with SelectedChart = chart}, Cmd.none

type NumberSeriesPoint =
    {
        Y : float
        X : float
    }

open Fable.React
open Fable.React.Props
open Fulma

[<Emit("""(function(x,y){return { name : x, uv : y};})($0,$1)""")>]
let create x y : obj = jsNative

let lineChart chart =
    let data =
        chart.Data
        |> Seq.head
        |> Seq.map(fun (d : Point) ->
            let y =
               match d.Y with
               Number f ->
                   f
               | v -> failwithf "Not a number %A" v
            create d.X y
        ) |> Array.ofSeq
    lineChart
        [ margin 5. 20. 5. 0.
          Chart.Width 600.
          Chart.Height 300.
          Chart.Data data ]
        [ line
            [ Cartesian.Type Monotone
              Cartesian.DataKey "uv"
              P.Stroke "#8884d8"
              P.StrokeWidth 2. ]
            []
          cartesianGrid
            [ P.Stroke "#ccc"
              P.StrokeDasharray "5 5" ]
            []
          xaxis [ Cartesian.DataKey "name"] []
          yaxis [] []
          tooltip [] []
        ]


let barChart chart =
    let data =
        chart.Data
        |> Seq.head
        |> Array.ofSeq
        |> Array.map(fun (d : Point) ->
            let y =
               match d.Y with
               Number f ->
                   f
               | v -> failwithf "Not a number %A" v
            create d.X y
        )
    composedChart
        [ margin 5. 20. 5. 0.
          Chart.Width 600.
          Chart.Height 300.
          Chart.Data data

           ]
        [ xaxis [Cartesian.DataKey "name"] []
          yaxis [] []
          tooltip [] []
          legend [
          ] []
          cartesianGrid [P.StrokeDasharray "3 3"] []
          bar [Cartesian.DataKey "pv"; Cartesian.StackId "a"; P.Fill "#8884d8"] []
          bar [Cartesian.DataKey "uv"; Cartesian.StackId "a"; P.Fill "#82ca9d"] []
          line
            [ Cartesian.Type Monotone
              Cartesian.DataKey "uv"
              P.Stroke "#8884d8"
              P.Fill "#FFF"
              P.StrokeWidth 2. ]
            []
        ]
(*
let areaChartSample() =
    areaChart
        [ margin 10. 30. 0. 0.
          Chart.Width 730.
          Chart.Height 250.
          Chart.Data data
          Chart.OnClick onMouseEvent ]
        [
          R.defs []
            [ R.linearGradient
                [ P.Id "colorUv"; P.X1 0.; P.Y1 0.; P.X2 0.; P.Y2 1.]
                [ R.stop [ P.Offset "5%"; P.StopColor "#8884d8"; P.StopOpacity 0.8 ] []
                  R.stop [ P.Offset "95%"; P.StopColor "#8884d8"; P.StopOpacity 0 ] [] ]
              R.linearGradient
                [ P.Id "colorPv"; P.X1 0.; P.Y1 0.; P.X2 0.; P.Y2 1.]
                [ R.stop [ P.Offset "5%"; P.StopColor "#82ca9d"; P.StopOpacity 0.8 ] []
                  R.stop [ P.Offset "95%"; P.StopColor "#82ca9d"; P.StopOpacity 0 ] [] ] ]
          xaxis [ Cartesian.DataKey "name" ] []
          yaxis [] []
          cartesianGrid [P.StrokeDasharray "3 3"] []
          tooltip [] []
          legend [
            Legend.OnClick onMouseEventIndexed
            Legend.OnMouseEnter onMouseEventIndexed
          ] []
          area
            [ Cartesian.Type Monotone
              Cartesian.DataKey "uv"
              Cartesian.Stroke "#8884d8"
              P.Fill "url(#colorUv)"
              P.FillOpacity 1 ] []
          area
            [ Cartesian.Type Monotone
              Cartesian.DataKey "pv"
              Cartesian.Stroke "#82ca9d"
              P.Fill "url(#colorPv)"
              P.FillOpacity 1 ] []
        ]

let pieChartSample() =
    pieChart
        [ margin 10. 30. 0. 0.
          Chart.Width 730.
          Chart.Height 250. ] [
          legend [
            Legend.OnClick onMouseEventIndexed
            Legend.OnMouseEnter onMouseEventIndexed
          ] []
          pie [
            Polar.Data polarData
            Polar.DataKey "value"
            Polar.Label true
            Polar.OnClick onMouseEventIndexed
            Polar.OnMouseEnter onMouseEventIndexed
            P.Fill "#8884d8"
          ] [
          ]
        ]
*)

let navBrand model dispatch =
    model.Areas
    |> List.map(fun area ->
        let createChartId areaId chartId =
            sprintf "%s/%s" areaId chartId
        let isActive =
            area.ChartIds |> List.tryFind(fun c -> createChartId area.Id c = model.SelectedChart.Id) |> Option.isSome

        Navbar.Item.div [
            Navbar.Item.HasDropdown
            Navbar.Item.Props [ OnClick(fun e ->
                (area.ChartIds |> List.head) |> createChartId area.Id |> ChartSelected |> dispatch
                e.preventDefault()
            ) ]
            Navbar.Item.IsActive isActive
        ] [
            Dropdown.dropdown [ Dropdown.IsHoverable; ]
                [ Dropdown.trigger [ ]
                    [ Button.button [ ]
                        [ span [ ]
                            [ str area.Id ]
                          Icon.icon [ Icon.Size IsSmall ]
                            [ Fa.i [ Fa.Solid.AngleDown ]
                                [ ] ] ] ]
                  Dropdown.menu [ ]
                    [ Dropdown.content [ ]
                         <| List.map(fun chartId ->
                                 let chartId = createChartId area.Id chartId
                                 let isActive = model.SelectedChart.Id = chartId
                                 Dropdown.Item.a [ Dropdown.Item.IsActive isActive;Dropdown.Item.Option.Props[OnClick(fun e ->
                                     chartId |> ChartSelected |> dispatch
                                     e.preventDefault()
                                 )]]
                                    [ str chartId ]
                            ) area.ChartIds
                  ]
            ]
        ]
    ) |> Navbar.Brand.div [ ]



let renderChart (chartModel : ChartModel)  =
    barChart chartModel

let containerBox (model : ChartModel) (dispatch : Msg -> unit) =
        Box.box' [ ] [
            Content.content [ ]
               [
                 Heading.p [Heading.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered)] ] [str model.Title ]
                 div [] [renderChart model]
               ]
        ]

let view (model : Model) (dispatch : Msg -> unit) =
    Hero.hero [
        Hero.Color IsPrimary
        Hero.IsFullHeight
        Hero.Props [
            Style [
                Background """linear-gradient(rgba(0, 0, 0, 0.5), rgba(0, 0, 0, 0.5)), url("https://unsplash.it/1200/900?random") no-repeat center center fixed"""
                BackgroundSize "cover"
            ]
        ]
    ] [
        Hero.head [ ] [
            Navbar.navbar [ ] [
                Container.container [ ] [ navBrand model dispatch ]
            ]
        ]

        Hero.body [ ] [
            Container.container [ ]
                [
                    Column.column [
                        Column.Width (Screen.All, Column.Is6)
                        Column.Offset (Screen.All, Column.Is3)
                    ] [ if model.SelectedChart.Data |> Seq.isEmpty |> not then yield containerBox model.SelectedChart dispatch ]
                ]
            ]
    ]
