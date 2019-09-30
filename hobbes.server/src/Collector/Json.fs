namespace Hobbes.Collector

open System
open FSharp.Data
open FSharp.Data.JsonExtensions

module Json = 
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
    let readJson records =                                  
        let _readJson readers (records : JsonValue []) =    
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
        _readJson Map.empty records