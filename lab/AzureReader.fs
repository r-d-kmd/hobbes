module Azure.Reader


open FSharp.Data
open System
open Hobbes.Parsing
open Hobbes.FSharp
open Hobbes.FSharp.DataStructures
open FSharp.Data.JsonExtensions

type AzureWorkItems = JsonProvider<"""[{"@odata.nextLink":"https"},{"@odata.nextLink":"https"}]""", SampleIsList = true>
type Cookies = JsonProvider<"""[["foo","bar"]]"""> //JsonProvider<"cookies.json">

let combineData matrix1 matrix2= 
    matrix1 |> Seq.fold(fun m (columnName,values) ->
        match m |> Map.tryFind columnName with
        None -> m.Add(columnName, values)
        | Some vs -> m.Add(columnName,vs |> Seq.append values)
    ) (matrix2 |> Map.ofSeq)
    |> Map.toSeq

let serialiseValue (value : obj) = 
    match value with
    :? string as s -> sprintf """ "%s" """ s
    | :? bool as b -> b |> string
    | :? int as i -> i |> string
    | :? float as f -> f |> string
    | :? DateTime as d -> sprintf """ "%s" """ (d.ToString())
    | _ -> sprintf "%A" value
    
let table2json table =  
    String.Join(",", table
                     |> List.ofSeq
                     |> List.map(fun (columnName,values) -> 
                        let valuesAsString =
                            System.String.Join(",", values 
                                                    |> Seq.map serialiseValue
                            )
                        sprintf """ "%s" : [%s] """ columnName valuesAsString
                     )
    ) |> sprintf "{%s}"

let rec determineReader (value : JsonValue) = 
    try 
       value.AsFloat() |> ignore
       fun (value : JsonValue) -> 
           try
               value.AsFloat()
           with _ ->
               0.0
           :> IComparable
    with _ ->
       try
           value.AsDateTime(Globalization.CultureInfo.CurrentCulture) |> ignore
           fun (value : JsonValue) ->
                value.AsDateTime(Globalization.CultureInfo.CurrentCulture) :> IComparable
       with _ ->
            try
                value.AsDateTimeOffset(Globalization.CultureInfo.CurrentCulture) |> ignore
                fun (value : JsonValue) ->
                    value.AsDateTimeOffset(Globalization.CultureInfo.CurrentCulture).DateTime
                    :> IComparable
            with _ ->
                try
                    value.AsBoolean() |> ignore
                    fun (value : JsonValue) -> 
                        value.AsBoolean()
                        :> IComparable 
                with _ ->
                    value.AsString() |> ignore
                    fun (value : JsonValue) -> 
                        value.AsString()
                        :> IComparable

let isObject (value : JsonValue) =
    value.Properties |> Array.isEmpty |> not 

let rec readObject (readers : Map<_,_>) (prefix : string) (value : JsonValue) =
    if value |> isObject then  
        value.Properties
        |> Array.fold(fun (readers, values) (name,value) ->
            let columnName = 
                if prefix.Length > 0 then
                   prefix + "." + name
                else name         
            let readers, vs = readObject readers columnName value
            readers,values@vs
        )(readers,[])
    else
        let readers, reader = 
            match readers |> Map.tryFind prefix with
            None -> 
                let reader = determineReader value
                readers.Add(prefix, reader), reader
            | Some reader -> readers, reader
        let readers,readValue = 
            try
                readers, reader value
            with _ -> 
                //most likely cause is inconsistent data types
                //let's replace the reader and procced with the new one
                let reader = determineReader value
                let readers = readers.Add(prefix,reader)
                readers, 
                    try
                        reader value
                    with e -> 
                        eprintfn "Failed reading value. MSG: %s. prefix: %s value: %A readers %A" e.Message prefix value readers
                        null
        readers,[(prefix,readValue)]
                                      
let readJson readers (records : JsonValue []) =    
    let rec addReader readers (prefix : string) (value : JsonValue) =
        let columnName name = 
            if prefix.Length > 0 then
               prefix + "." + name
            else name         
        if value |> isObject then  
            value.Properties
            |> Array.filter(fun (name,_) -> 
               let col = columnName name
               readers 
               |> Map.containsKey col 
               |> not
            ) |> Array.fold(fun readers (name,value) ->  
                addReader readers (columnName name) value
            ) readers
        else
            let reader =  determineReader value
            readers.Add(prefix, reader)
    let readers, result= 
        try
            let readers, result= 
                records
                |> Seq.fold(fun (readers,values) record -> 
                    let readers,vs = readObject readers "" record
                    readers, values@vs
                ) (readers,[])
            readers, result |> Seq.ofList
        with e ->
            eprintfn "Failed to read data %s" e.Message
            readers, Seq.empty
    readers,
    result
    |> Seq.groupBy fst
    |> Seq.map(fun (columnName, values) -> 
        columnName, 
            values 
            |> Seq.map snd
    )
    
let getCachedFiles dir projectName = 
    IO.Directory.EnumerateFiles (dir, sprintf """%s_full_*.json""" projectName)

let writeFile dir (fileName,content) = 
    IO.File.WriteAllTextAsync(dir + "/" + fileName,content) 
    |> Async.AwaitTask
    |> Async.Start
        
let rec private readRecord (stopwatch : Diagnostics.Stopwatch) projectName url recordCount acc = 
        async {
            let dataFile =  sprintf """%s_full_%d.json""" projectName recordCount
            let start = stopwatch.ElapsedMilliseconds
            let! dataJson = 
                printfn "Requesting %s" url
                Http.AsyncRequestString(url,
                    httpMethod = "GET",
                    cookies = 
                        (Cookies.GetSamples()
                         |> Array.map(function
                                       [|name;value|] -> name,value
                                       | a -> failwithf "No idead what to do with: %A" a
                         ))
                )
            printfn "Data received in %ims" (stopwatch.ElapsedMilliseconds - start)
            writeFile "data" (dataFile,dataJson) 
            let nextlink = (AzureWorkItems.Parse dataJson).OdataNextLink
            if String.IsNullOrWhiteSpace(nextlink) |> not then
                return! readRecord stopwatch projectName nextlink (recordCount + 1) ((dataFile,dataJson)::acc)
            else
                return (dataFile,dataJson)::acc
        }

let read stopwatch projectName (modelling : IDataMatrix -> IDataMatrix) : Async<IDataMatrix> =
    let convertedDataFiles = 
        getCachedFiles "results" projectName

    let getNumberFromFile (filename : string) = 
        match System.Int32.TryParse ((IO.Path.GetFileNameWithoutExtension filename).Split('_') |> Array.last) with
        false, _ -> -1
        | _ , i -> i

    let lastConversion = 
        if convertedDataFiles |> Seq.isEmpty then -1
        else
            convertedDataFiles
            |> Seq.map(getNumberFromFile)
            |> Seq.sortDescending 
            |> Seq.head

    let dataFiles =  
        getCachedFiles "data" projectName 
        |> Seq.sortByDescending getNumberFromFile

    let createIndex segmentNumber rowNumber = 
        sprintf "%d-%d" segmentNumber rowNumber //this is a unique int across the records making it possible to align columns
        |> AST.KeyType.Create
    async {
        let! url,stableDataForConversion = 
          
            if dataFiles |> Seq.isEmpty then
               async {
                    let url = 
                        projectName
                        |> sprintf "https://analytics.dev.azure.com/kmddk/%s/_odata/v2.0/WorkItemRevisions?%%24expand=Iteration&%%24filter=IsLastRevisionOfDay%%20eq%%20true&orderby=Iteration.StartDate%%20desc" 
                    return (url,[])
                }
            else
                async {
                    //last one is likely incomplete, so skip that one
                    let stable' = dataFiles |> Seq.tail
                    //The last but on is stable and we should get the nextlink from this one and proceed
                    let lastStableDataFile = stable' |> Seq.head

                    //Any data file that hasn't got a corresponding converted data file should be piped through the modelling
                    let stableDataForConversion = 
                        stable' 
                        |> Seq.tail
                        |> Seq.filter(fun f ->
                            f |> getNumberFromFile > lastConversion
                        )

                    let url = 
                        (lastStableDataFile
                         |> IO.File.ReadAllText
                         |> AzureWorkItems.Parse).OdataNextLink
                    return url,(stableDataForConversion |> Seq.toList)
                }
    
        let start = (dataFiles |> Seq.length) - 1 |> max 0
        let! newDataFiles = readRecord stopwatch projectName url start []
        let numberOfCachedSegments = convertedDataFiles |> Seq.length
        let parseRecord readers segmentNumber json =
            let readers, table = 
                (json |> JsonValue.Parse).["value"].AsArray()
                |> readJson readers
                
            let data = 
                table
                |> Seq.map(fun (columnName, values) ->
                    columnName, values
                                |> Seq.mapi(fun rowNumber v ->
                                     createIndex (segmentNumber) rowNumber, v
                                )
                )
            readers, data
        
        
        let readers,newDataColumns = 
            newDataFiles
            |> Seq.indexed
            |> Seq.fold(fun (readers,result) (segmentNumber, (fileName,json)) ->
                printfn "Parsing new record %s" fileName
                let st = stopwatch.ElapsedMilliseconds
                let readers,columns = parseRecord readers (segmentNumber + numberOfCachedSegments) json
                printfn "%s parsed in %dms" fileName (stopwatch.ElapsedMilliseconds - st)
                readers,columns::result
            ) (Map.empty,[])
            
        let numberOfSegments = (newDataColumns |> List.length) + numberOfCachedSegments
        let lastFile = 
            newDataFiles
            |> Seq.sortByDescending(fun (f,_) -> 
                getNumberFromFile f
            )|> Seq.head
            |> fst

        let! stableFilesColumns = 
            stableDataForConversion
            |> Seq.mapi(fun segmentNumber fileName ->
                async {
                    let st = stopwatch.ElapsedMilliseconds
                    let! json = IO.File.ReadAllTextAsync fileName |> Async.AwaitTask
                    printfn "Parsing %s" fileName
                    let _,columns = parseRecord readers (segmentNumber + numberOfSegments) json
                    printfn "%s parsed in %dms" fileName (stopwatch.ElapsedMilliseconds - st)
                    return columns
                }
            ) |> Async.Parallel


        let completeTable = 
            (stableFilesColumns |> List.ofArray)@newDataColumns
            |> Seq.fold(fun map table -> 
                table
                |> Seq.fold(fun map (columnName, values) ->
                    match map |> Map.tryFind columnName with
                    None -> map.Add(columnName, values)
                    | Some vs -> map.Add(columnName, values |> Seq.append vs)
                ) map
            ) Map.empty
            |> Map.toSeq
            
        printfn "Table created with %d columns" (completeTable |> Seq.length)
        
        let matrix = 
            completeTable
            |> DataMatrix.fromTable
        
        printfn "Matrix created"

        let result = 
            matrix
            |> modelling

        printfn "Writing results"

        writeFile "results" (lastFile,result.AsJson())
        return result
    }
        