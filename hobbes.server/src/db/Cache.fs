module Cache
let store cacheKey (data : string) =
    let record = 
            sprintf """{
                "TimeStamp" : "%s",
                "Data" : %s
            }""" (System.DateTime.Now.ToString (System.Globalization.CultureInfo.CurrentCulture)) 
                 data
    try
        Database.cache.Put cacheKey record |> ignore
    with e ->
        eprintfn "Failed to cahce data. Reason: %s" e.Message
    (Database.CacheRecord.Parse record).Data.ToString()


let createRecord cacheKey (data : string) = 
    let record = 
            sprintf """{
                "TimeStamp" : "%s",
                "Data" : %s
            }""" (System.DateTime.Now.ToString (System.Globalization.CultureInfo.CurrentCulture)) 
                 (data.Replace("\\", "\\\\")) //escape special json characters
    (Database.CacheRecord.Parse record).Data

let tryRetrieve cacheKey =
    Database.cache.TryGet cacheKey
    |> Option.bind(fun cacheRecord -> 
        printfn "Retrieved %s from cache" cacheKey
        cacheRecord.Data.ToString() |> Some
    )

let retrieve cacheKey =
   (Database.cache.Get cacheKey).Data.ToString()