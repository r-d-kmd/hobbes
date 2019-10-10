namespace Hobbes.FSharp

open Deedle
open Accord.MachineLearning
open Hobbes.Parsing
open System
open Accord.Statistics.Models.Regression.Linear

module Clustering = 
    let withKeys colName (frame : Frame<AST.KeyType,_>) = 
            frame?(colName) <- 
                frame.RowKeys
                |> Seq.map(fun k ->
                    match AST.KeyType.UnWrap k with
                    :? (obj list) as lst ->
                        match lst with
                        [a] -> a
                        | a::b::c::_ -> (a,b,c) :> obj
                        | a::b::_ -> (a,b) :> obj
                        | _ -> lst :> obj
                    | a -> a
                )
            frame
    let private numericTypes =
        [
            typeof<Int16>
            typeof<int>
            typeof<Int64>
            typeof<Decimal>
            typeof<Single>
            typeof<Double>
        ]

    let private isNumeric v = 
        let t = v.GetType()
        numericTypes
        |> List.contains t


    let private reattachGroupedColumns columnNames (grouped : Frame<_,_>) =
        let keys = 
            grouped.RowKeys
            |> Seq.map(AST.KeyType.UnWrap >> (fun k ->
                match k with
                :? (obj list) as l -> l
                | a -> [a]
            ))

        columnNames
        |> List.indexed
        |> List.fold(fun frame (i,columnName) ->
            let groupSeries =
                keys
                |> Seq.map(fun ks ->
                       ks |> List.item i
                ) |> Seq.zip grouped.RowKeys
                |> series
            frame
            |> Frame.addCol
                columnName
                groupSeries
                
        ) grouped
        |> Frame.denseCols
        |> Frame.ofColumns

    let internal reduceGroup reduction columnNames (frame : Frame<_,_>)  = 
         //we're removing group columns
        let allOther = 
           frame 
           |> Frame.cols
           |> Series.keys
           |> Seq.filter(fun columnName ->
              columnNames
              |> List.tryFind(fun c -> c = columnName)
              |> Option.isNone
           )
        let red =
            match reduction with
            AST.Sum-> 
                Stats.levelSum fst
            | AST.Count  -> 
                (Stats.levelCount fst)
                >> (Series.mapValues float)
            | AST.Median -> 
                Stats.levelMedian fst
            | AST.Mean-> 
                Stats.levelMean fst
            | AST.StdDev-> 
                Stats.levelStdDev fst
            | AST.Variance-> 
                Stats.levelVariance fst
            | AST.Max-> 
                Series.applyLevel fst (Stats.max)
            | AST.Min-> 
                Series.applyLevel fst (Stats.min)

        frame
        |> Frame.sliceCols allOther
        |> Frame.getNumericCols
        |> Series.mapValues red
        |> Frame.ofColumns
        |> reattachGroupedColumns columnNames

    let internal group columnNames reducer (frame : Frame<AST.KeyType,string>) : Frame<AST.KeyType,string> = 
        frame 
        |> Frame.groupRowsUsing(fun _ row -> 
            columnNames   
            |> List.map(fun columnKey -> 
                match row.TryGet columnKey with
                | OptionalValue.Missing -> AST.Missing
                | OptionalValue.Present(value) -> 
                    AST.KeyType.Create value
            )|> AST.List
        ) |> reducer columnNames
       
    let internal kmeans clusters transform frame = 
        let rows = 
            frame
            |> Frame.getNumericCols
            |> Frame.ofColumns
            |> Frame.getRows
            |> Series.mapValues(fun row -> 
                row
                |> Series.mapValues(float)
                |> Series.observations
                |> Seq.map snd
                |> Array.ofSeq
            )
            |> Series.observations
            |> Array.ofSeq

        let values = 
            rows
            |> Array.map snd 

        let algorithm = KMeans(clusters)
        let clusters = 
            algorithm.Learn(values)
            
        let lookupTable = 
           clusters.Decide(values)
           |> Array.zip (rows |> Array.map fst)

        let clusterLookup key = 
           lookupTable
           |> Array.find(fun (k,_) -> k = key)
           |> fst

        frame?cluster <-
            frame
            |> Frame.mapRows(fun rowKey _ ->
                clusterLookup rowKey
            )

        frame
        |> group ["cluster"]  (reduceGroup transform) 

    let internal counting buckets transform frame = 
        let frame = 
            frame
            |> Frame.indexRowsOrdinally
        let inEach = frame.RowCount / buckets
        frame?bucket <-
            frame
            |> Frame.mapRows(fun k _ -> k / inEach)
        
        frame
        |> Frame.mapRowKeys AST.KeyType.Create 
        |> group ["bucket"] (reduceGroup transform)
        

           
    let inline internal distinct transformation (frame : Frame<_,_>) = 
         counting frame.RowCount transformation frame

module DataStructures =
    type JsonTableFormat = 
        Column
        | Row
        | Csv
    type IDataMatrix = 
        abstract Transform : AST.Expression -> IDataMatrix
        abstract Combine : IDataMatrix -> IDataMatrix
        abstract ToJson : JsonTableFormat -> string
        abstract RowCount : int with get
    
    type private Comp = System.IComparable

    type DataMatrix (frame : Frame<AST.KeyType,string>) =
        
        let keySeries = 
            if frame.ColumnCount > 0 then
                frame
                |> Frame.getCols
                |> Series.observations
                |> (Seq.head >> snd)
                |> Series.map(fun k _ -> (k |> AST.KeyType.UnWrap) :?> Comp)
            else
                [] |> series

        let rec compileExpression frame expr : Series<AST.KeyType,Comp> -> Series<AST.KeyType,Comp> = 
            let compileExpression = compileExpression frame
            let columnComputationExpression expression (series : Series<AST.KeyType,Comp>) : Series<AST.KeyType,Comp>= 
                let aggregate columnExpression (f : Series<AST.KeyType,Comp> -> Series<AST.KeyType,float>) = 
                         series
                         |> columnExpression
                         |> f
                         |> Series.mapValues(fun v -> v :> Comp)
                     
                match expression with
                AST.Moving(reduction, windowSize, columnExpression) -> 
                    let aggregate = aggregate  (compileExpression columnExpression)
                    let f =    
                        match reduction with 
                        AST.Sum -> 
                            aggregate (Stats.movingSum windowSize)
                        | AST.Count -> 
                             aggregate (Stats.movingCount windowSize)
                        | AST.Median -> 
                             failwith "can't use median as a moving stat"
                        | AST.Mean -> 
                             aggregate (Stats.movingMean windowSize)
                        | AST.StdDev -> 
                             aggregate (Stats.movingStdDev windowSize)
                        | AST.Variance -> 
                             aggregate (Stats.movingVariance windowSize)
                        | AST.Max -> 
                             aggregate (Stats.movingMax windowSize)
                        | AST.Min -> 
                             aggregate (Stats.movingMin windowSize)
                    f 
                | AST.Expanding(reduction, columnExpression) -> 
                    let aggregate = aggregate (compileExpression columnExpression)
                    match reduction with 
                    AST.Sum -> 
                        aggregate Stats.expandingSum 
                     | AST.Count -> 
                         aggregate Stats.expandingCount 
                     | AST.Median -> 
                         failwith "can't use median as an expanding stat"
                     | AST.Mean -> 
                         aggregate Stats.expandingMean 
                     | AST.StdDev -> 
                         aggregate Stats.expandingStdDev 
                     | AST.Variance -> 
                         aggregate Stats.expandingVariance 
                     | AST.Max -> 
                         aggregate Stats.expandingMax 
                     | AST.Min -> 
                         aggregate Stats.expandingMin 
            match expr with
            AST.Number n ->
               let n = 
                   match n with
                     AST.Int32 n -> n :> Comp
                     | AST.Int64 n -> n :> Comp
                     | AST.Float n -> n :> Comp
               Series.mapValues(fun _ -> n)
            | AST.DateTime d ->
               Series.mapValues(fun _ -> d :> Comp)           
            | AST.MissingValue ->
               Series.mapValues(fun _ -> null )
            | AST.String s ->
                  Series.mapValues(fun _ -> s :> Comp )          
            | AST.Keys ->
                  Series.keys
                  >> Seq.map(fun key ->
                      key,
                      match key |> AST.KeyType.UnWrap with
                       :? (obj list) as lst ->
                           match lst with
                           [a] -> a :?> Comp
                           | _ -> failwith "Can't use list"
                       | o -> o :?> Comp) 
                  >> series
            | AST.Binary(lhs,rhs, op) -> 
                let lhsExp =
                    fun series -> 
                        compileExpression lhs series
                        |> Series.mapValues(fun v -> v :?> float)
                let rhsExp = 
                    fun series -> 
                        compileExpression rhs series
                        |> Series.mapValues(fun v -> v :?> float)
                let f : Series<AST.KeyType,float> -> Series<AST.KeyType,float> -> Series<AST.KeyType,float> = 
                    match op with
                    AST.Addition -> (+)
                    | AST.Subtraction -> (-)
                    | AST.Multiplication -> (*)
                    | AST.Division -> (/)
                fun series ->
                    f <| lhsExp series <| (rhsExp series)
                    |> Series.mapValues(fun v -> v :> Comp)
            | AST.ColumnName name ->
                 fun _ -> 
                     let series = 
                         frame
                         |> Frame.getCol name
                     series
            | AST.Int exp ->
                fun s ->
                    s |> (compileExpression exp)
                    |> Series.mapValues(fun v -> int(v :?> float) :> Comp)
            | AST.IfThisThenElse(condition,thenBody,elseBody) ->
                let conditionExp = 
                    compileBooleanExpression condition
                let thenBodyExp = compileExpression thenBody
                let elseBodyExp = compileExpression elseBody
                
                fun series ->
                    Frame(
                        ["__if__";"__then__";"__else__"], 
                        [
                         conditionExp
                         thenBodyExp 
                         elseBodyExp
                        ]|> List.map(fun f -> f series :> ISeries<_>)
                    ) |> Frame.mapRowValues(fun row -> 
                        if row.GetAs<bool> "__if__" then row.GetAs<Comp> "__then__" else row.GetAs<Comp> "__else__"
                    )
            | AST.FormatDate(columnName, format)  ->
                let column = frame.GetColumn<obj> columnName
                fun _ ->
                    column
                    |> Series.mapValues(fun v ->
                    let date = 
                        match v with
                        :? System.DateTimeOffset as d -> Some d.DateTime
                        | :? string as s -> 
                            (s
                             |> System.DateTimeOffset.Parse).DateTime
                            |> Some
                        | _ -> None

                    let formatter (date : System.DateTime) =
                            match format with
                            AST.Year -> date.Year :> System.IComparable
                            | AST.Month -> date.Month :> System.IComparable
                            | AST.Day -> date.Day :> System.IComparable
                            | AST.Date -> date.Date :> System.IComparable
                            | AST.Weekday -> date.DayOfWeek.ToString() :> System.IComparable
                            | AST.Week -> (date.DayOfYear / 7) :> System.IComparable
                    date
                    |> Option.bind (formatter >> Some)
                    |> Option.orElse(Some null)
                    |> Option.get
                )
                    
            | AST.Boolean b ->
                compileBooleanExpression b
            | AST.ColumnExpression(expression) ->  
                columnComputationExpression expression
            
            | AST.Regression(regressionType,inputTreeNodes,outputTreeNodes) ->
                let inputExpr = 
                    inputTreeNodes
                    |> compileExpression
                let outputExprs = 
                    outputTreeNodes
                    |> compileExpression
                
                fun s ->
                    let transformExpressionsToVariants expr = 
                        s
                        |> expr
                        |> Series.mapValues(fun c -> c :> obj :?> float)
                        |> Series.values
                        |> Array.ofSeq

                    let inputs = 
                        inputExpr
                        |> transformExpressionsToVariants
                    
                    let outputs = 
                        outputExprs
                        |> transformExpressionsToVariants
                        
                    match regressionType with
                    AST.Linear ->
                        let ols = OrdinaryLeastSquares()
                        let regression = ols.Learn(inputs, outputs)    
                        regression.Transform(inputs)
                        |> Array.zip (s
                                     |> Series.keys
                                     |> Seq.toArray)
                        |> series
                        |> Series.mapValues(fun c -> c:> Comp)

        and compileTempColumn defaultName exp =
               match exp with
               AST.ComputationExpression.ColumnName name -> 
                   name, (fun _ _ -> ())   
               | _ ->
                   defaultName, 
                       (fun frame keySeries ->
                           frame?(defaultName) <- (compileExpression frame exp keySeries))
                   
        
        and compileBooleanExpression exp : Series<AST.KeyType,Comp> -> Series<AST.KeyType,Comp> = 
            let binaryOp op lhs rhs =
                 fun series -> 
                   
                     let lhsSerie = lhs series
                     let rhsSerie = rhs series
                     let frame = 
                        Frame(["lhs";"rhs"],
                                [
                                    lhsSerie 
                                    rhsSerie
                                ])
                     frame
                     |> Frame.mapRowValues(fun row ->
                         let lhs = row.GetAs<Comp> "lhs"
                         let rhs = row.GetAs<Comp> "rhs"
                         let res = op lhs rhs
                         res :> Comp
                     )
            match exp with
            AST.Not e -> 
                let exp = compileBooleanExpression e
                fun series -> 
                    exp series
                    |> Series.mapValues(fun c -> 
                        c:?> bool |> not :> Comp)
            | AST.And(e1,e2) ->
                let exp1 = compileBooleanExpression e1
                let exp2 = compileBooleanExpression e2
                binaryOp (fun exp1 exp2 -> exp1 :?> bool && exp2 :?> bool) exp1 exp2
            | AST.Or(e1,e2) ->
                let exp1 = compileBooleanExpression e1
                let exp2 = compileBooleanExpression e2
                binaryOp (fun exp1 exp2 -> exp1 :?> bool || exp2 :?> bool) exp1 exp2
            | AST.Comparison(lhs,rhs,op) ->
                let opExp : System.IComparable -> System.IComparable -> bool =                
                    match op with
                    AST.GreaterThan -> (>)        
                    | AST.GreaterThanOrEqual -> (>=) 
                    | AST.LessThan -> (<)           
                    | AST.LessThanOrEqual -> (<=)    
                    | AST.EqualTo -> (=)
                    | AST.Contains ->
                        fun lhs rhs ->
                            let lhsString = lhs |> string
                            let rhsString = rhs |> string
                            if String.IsNullOrEmpty(lhsString) then false
                            elif String.IsNullOrEmpty(rhsString) then true
                            else
                               lhsString.IndexOf(rhsString, StringComparison.CurrentCultureIgnoreCase) >= 0
                let lhsExp = compileExpression frame lhs
                let rhsExp = compileExpression frame rhs
                binaryOp opExp lhsExp rhsExp
            | AST.ValueOfColumn c ->
                fun _ ->
                    frame
                    |> Frame.getCol c
                    |> Series.mapValues(fun c -> c :> Comp)
                    
        let column columnExpression =
            match columnExpression with
            | AST.RenameColumn(orgColumnName, newColumnName) ->
                frame
                |> Frame.mapColKeys(fun columnName -> 
                    if columnName = orgColumnName then newColumnName else columnName
                )
            | AST.CreateColumn (exp, nameOfNewColumn) -> 
                let compiledExpression = compileExpression frame exp
                frame?(nameOfNewColumn) <- (compiledExpression (keySeries))
                   
                frame
            | AST.Pivot(rowKeyExpression,columnKeyExpression,valueExpression, reduction) ->
                let rowkey,compiledExpressionFunc = compileTempColumn "__rowkey__" rowKeyExpression
                compiledExpressionFunc frame keySeries
                let columnkey,compiledExpressionFunc = compileTempColumn "__columnkey__" columnKeyExpression 
                compiledExpressionFunc frame keySeries

                frame
                |> Frame.pivotTable 
                    (fun _ r -> 
                        (r.TryGet rowkey).ValueOrDefault
                        |> AST.KeyType.Create)
                    (fun _ r -> (r.TryGet columnkey).ValueOrDefault |> string)
                    (fun f ->
                          let resultsColumn,compiledExpressionFunc = compileTempColumn "__result__" valueExpression 
                          compiledExpressionFunc f keySeries
                          f.GetColumn resultsColumn
                          |>(match reduction with
                             AST.Count -> 
                                  Series.countValues >> float
                             | AST.Sum -> 
                                  Stats.sum
                             | AST.Median -> 
                                  Stats.median
                             | AST.Mean -> 
                                  Stats.mean
                             | AST.StdDev -> 
                                  Stats.stdDev
                             | AST.Variance -> 
                                  Stats.variance
                             | AST.Max -> 
                                  Stats.max
                             | AST.Min -> 
                                  Stats.min ))
        let reduce reduction = 
            let aggregate f = 
                frame
                |> Frame.getNumericCols
                |> Series.observations
                |> Seq.map(fun (k, v) -> 
                    k, [AST.KeyType.Create 0 => (f v :> Comp) ] |> series
                ) |> Frame.ofColumns
                |> DataMatrix
                :> IDataMatrix

            match reduction with
            AST.Sum -> 
                aggregate Stats.sum
            | AST.Count -> 
                aggregate Stats.count
            | AST.Median -> 
                aggregate Stats.median
            | AST.Mean -> 
                aggregate Stats.mean
            | AST.StdDev -> 
                aggregate Stats.stdDev
            | AST.Variance -> 
                aggregate Stats.variance
            | AST.Max -> 
                aggregate Stats.max
            | AST.Min -> 
                aggregate Stats.min 
        
        let filterAndSort filter =
            let f = 
                match filter with
                AST.SliceColumns cols ->
                    Frame.sliceCols cols
                | AST.IndexBy exp ->
                    fun frame ->
                        let columnName = 
                            match exp with
                            AST.ColumnName name ->
                                //Lets use the optimized verstion if it's a column and not a calcultaed expression
                                name
                            | _ -> 
                                let indexByColumnName ="__index__"
                                let exp = compileExpression frame exp
                                frame?(indexByColumnName) <- 
                                    (exp keySeries)
                                indexByColumnName
                        let res = 
                            frame
                            |> Frame.indexRows columnName
                            |> Frame.mapRowKeys(AST.KeyType.Create)
                        res
                | AST.SortBy columnName ->
                     Frame.sortRows columnName
                | AST.DenseRows->
                     Frame.denseRows
                     >> Frame.ofRows
                | AST.DenseColumns ->
                     Frame.denseCols
                     >> Frame.ofColumns
                | AST.NumericColumns->
                     Frame.getNumericCols
                     >> Frame.ofColumns
                | AST.Only condition ->
                    fun frame ->
                        let conditionColumn = "__condition__"
                        frame?(conditionColumn) <- compileBooleanExpression condition keySeries
                        frame
                        |> Frame.filterRows(fun _ row ->
                            (row.TryGetAs<bool> conditionColumn).ValueOrDefault
                        )
                        |> Frame.dropCol conditionColumn
                
            frame |> f
        let cluster c  =
            let f = 
                match c with
                 | AST.Buckets(b,reduction) -> Clustering.counting b reduction 
                 | AST.KMeansClustering (b,reduction) -> Clustering.kmeans b reduction 
                 | AST.Distinct reduction ->
                        Clustering.distinct reduction 
                 | AST.GroupBy(columnNames, groupReduction) ->
                     let reducer = 
                         match groupReduction with
                         | AST.Reduce reduction -> (Clustering.reduceGroup reduction)
                         | AST.Select selector ->
                             let select expression f _ grouped = 
                                    let selectorKey, compiledExpressionFunc = compileTempColumn "__selector__" expression
                                    let cols = 
                                        grouped
                                        |> Frame.getCols
                                        |> Series.observations
                                        |> Seq.map fst
                                    let values = 
                                        grouped
                                        |> Frame.nest
                                        |> Series.observations
                                        |> Seq.map (fun (_, subFrame) -> 
                                            let keySeries = 
                                                subFrame
                                                |> Frame.getCols
                                                |> Series.observations
                                                |> (Seq.head >> snd)
                                                |> Series.map(fun k _ -> (k |> AST.KeyType.UnWrap) :?> Comp)
                                            compiledExpressionFunc subFrame keySeries
                                            let rowKey,_ = 
                                                  subFrame.GetColumn selectorKey
                                                  |> Series.observations
                                                  |> f snd
                                            rowKey, subFrame |> Frame.getRow rowKey
                                        ) |> Frame.ofRows
                                    //remove the selector column    
                                    values
                                    |> Frame.sliceCols cols
                             match selector with
                             AST.MaxBy expression ->
                                 select expression (Seq.maxBy)
                             | AST.MinBy expression ->
                                 select expression (Seq.minBy)
                     Clustering.group columnNames reducer 
            f frame

        let toTable frame = 
            frame
            |> Frame.cols
            |> Series.observations
            |> Seq.map(fun (columnName, values) -> columnName, values |> Series.observations)

        let serialiseValue (_,value : obj) = 
            match value with
            :? string as s -> sprintf """ "%s" """ s
            | :? bool as b -> b |> string
            | :? int as i -> i |> string
            | :? float as f -> f |> string
            | :? DateTime as d -> sprintf """ "%s" """ (d.ToString())
            | _ -> sprintf "%A" value

        member private ___.Columns 
            with get() =
                frame
                |> Frame.cols
                |> Series.observations
                |> Seq.map(fun (columnName,values) ->
                    columnName, values 
                                |> Series.observationsAll
                                |> Seq.map(function
                                             k,None -> k,null
                                             | k,Some s -> k,s) 
                )
        member private ___.Frame with get() = frame
        
        member ___.AsTable() =
                frame
                |> Frame.cols
                |> Series.observations
                |> Seq.map(fun (columnName,values) ->
                    columnName,
                    values
                    |> Series.observations
                    |> Seq.map(fun (key,v) -> key,v :?> IComparable)
                )

        interface IDataMatrix with
            member ___.RowCount = frame.RowCount
            member ___.Combine other =
                let matrix1 = 
                    frame |> toTable
                let matrix2 =
                    (other :?> DataMatrix).Frame
                    |> toTable

                matrix1 |> Seq.fold(fun m (columnName,values) ->
                    match m |> Map.tryFind columnName with
                    None -> m.Add(columnName, values)
                    | Some vs -> m.Add(columnName,vs |> Seq.append values)
                ) (matrix2 |> Map.ofSeq)
                |> Map.toSeq
                |> Seq.map(fun (columnName,values) -> columnName, values |> series)
                |> series
                |> Frame.ofColumns
                |> DataMatrix
                :> IDataMatrix

            member this.Transform expr =
                match expr with
                AST.Reduction reduction -> 
                    reduce reduction
                | AST.FilterAndSorting fs ->
                    filterAndSort fs
                    |> DataMatrix
                    :> IDataMatrix
                | AST.Cluster c ->
                    cluster c
                    |> DataMatrix
                    :> IDataMatrix
                | AST.Column c ->
                    column c
                    |> DataMatrix
                    :> IDataMatrix
                | AST.NoOp -> 
                    this
                    :> IDataMatrix
            member __.ToJson format =
                match format with
                Column -> 
                    let table = 
                        frame
                        |> toTable

                    let columnNames = 
                        String.Join(",",table |> Seq.map (fst >> sprintf "%A")) |> sprintf "[%s]"

                    let values =
                        (",",table
                            |> Seq.map(fun (_,values) ->
                                System.String.Join(",", values 
                                                        |> Seq.map serialiseValue
                                ) |> sprintf "[%s]"
                            )) |> String.Join |> sprintf "[%s]"

                    sprintf """{"columnNames": %s, "values" : %s}""" columnNames values
                | Row ->
                    System.String.Join(",",
                        frame
                        |> Frame.rows
                        |> Series.observations
                        |> Seq.map(fun (_,row) ->
                            System.String.Join(",",
                                row
                                |> Series.observations
                                |> Seq.map(fun (columnName, value) ->
                                   sprintf """ "%s":%A""" columnName value 
                                )
                            ) |> sprintf "{%s}"
                        )
                     ) |> sprintf "[%s]"
                | Csv ->
                    ("\r\n",frame
                         |> Frame.getCols
                         |> Series.observations
                         |> Seq.map(fun (columnName, values) ->
                             columnName::(values
                                           |> Series.observationsAll
                                           |> Seq.map(fun (_,v) -> 
                                               match v with
                                               None -> ""
                                               | Some v -> v.ToString().Replace(":",";")
                                           ) |> List.ofSeq)
                         ) |> Seq.transpose
                         |> Seq.map(fun s -> System.String.Join(":",s)))
                    |> System.String.Join

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module DataMatrix =
        let empty = DataMatrix(Frame([],[]))
            
        let fromTable table =
                let frame = 
                    table
                    |> Seq.map(fun (columnName, values) ->
                        columnName,
                        values |> series
                    ) |> series
                    |> Frame.ofColumns
                frame
                |> DataMatrix
                :> IDataMatrix