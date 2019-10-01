open Saturn
open Giraffe.Core
open Giraffe.ResponseWriters
open Microsoft.AspNetCore.Http
open Hobbes.FSharp.DataStructures
open Database
open Hobbes.Server.Db
open System.Security.Cryptography
open System.IO

let private port = 
    match env "port" with
    null -> 8085
    | p -> int p

[<Literal>]
let private Basic = "basic "
let private encoding = System.Text.Encoding.UTF8

let private getBytes (s:string)= 
    encoding.GetBytes s

let private toB64 s = 
    System.Convert.ToBase64String s

let private fromB64 s = 
    System.Convert.FromBase64String s
    |> encoding.GetString

type private JwtPayload = FSharp.Data.JsonProvider<"""{"name":"some"}""">
let private keySuffix = System.Environment.GetEnvironmentVariable("KEY_SUFFIX")
let private getSignature personalKey header payload = 
    let hmac = System.Security.Cryptography.HMAC.Create("HMACSHA256")
    hmac.Key <- 
        personalKey + keySuffix
        |> getBytes

    payload
    |> getBytes
    |> toB64
    |> sprintf "%s.%s" header
    |> getBytes
    |> hmac.ComputeHash
    |> toB64

let private cryptoKey = 
       let keySize = 32
       [|for i in 0..keySize - 1 -> keySuffix.[i % keySuffix.Length]|]
       |> System.String
       |> getBytes

let private initializationVector = 
       [|for i in cryptoKey.Length..(cryptoKey.Length + 15) -> keySuffix.[i % keySuffix.Length]|]
       |> System.String
       |> getBytes

let encrypt (plainText : string) = 
    use rijAlg = new RijndaelManaged()
    rijAlg.Key <- cryptoKey
    rijAlg.IV <- initializationVector
    let encryptor = rijAlg.CreateEncryptor(rijAlg.Key, rijAlg.IV)
    use msEncrypt = new MemoryStream()
    (
        use csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write)
        use swEncrypt = new StreamWriter(csEncrypt)
        swEncrypt.Write(plainText)
    )
    msEncrypt.Close()
    msEncrypt.ToArray()
    |> toB64

let decrypt (base64Text : string) = 
    let cipherText = base64Text |> System.Convert.FromBase64String
    use rijAlg = new RijndaelManaged()
    rijAlg.Key <- cryptoKey
    rijAlg.IV <- initializationVector
    let decryptor = rijAlg.CreateDecryptor(rijAlg.Key, rijAlg.IV)
    use msDecrypt = new MemoryStream(cipherText)
    (
        use csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read)
        use srDecrypt = new StreamReader(csDecrypt)
        srDecrypt.ReadToEnd()
    )

let private createToken (user : UserRecord.Root) = 
    let header = 
        """{"alg":"HS256","type":"JWT"}"""
        |> getBytes
        |> toB64
    let payload = sprintf """{"name": "%s"}""" user.Name
    let signature = getSignature user.DerivedKey header payload
    sprintf "%s.%s.%s" header payload signature
    |> encrypt

let private verifyToken (token : string) = 
    match token.Split('.') with
    [|header;payload;signature|] ->
        let jwtPayload = JwtPayload.Parse payload
        let user = users.Get (sprintf "org.couchdb.user:%s" jwtPayload.Name)
        signature = getSignature user.DerivedKey header payload
    | _ -> 
        eprintfn "Tried to gain access with %s" token
        false

let private enumeratorMap f (e : System.Collections.IEnumerator) = //Shouldn't be here
    let rec aux acc =
        match e.MoveNext() with
        true -> aux (f e.Current :: acc)
        | false -> acc
    aux []
      

let private data configurationName =
    
    fun func (ctx : HttpContext) ->
        let statusCode, body =  
            match ctx.TryGetRequestHeader "Authorization" with
            None ->    
                eprintfn "Tried to gain access without a key"
                403, "Unauthorized"
            | Some authToken ->
                let key =
                    if authToken.ToLower().StartsWith(Basic) then
                        authToken.Substring(Basic.Length)
                        |> fromB64
                        |> (fun s -> 
                            let apiKey = s.Substring(0,s.Length - 1) //skip the last character ':'
                            printfn "Using key: %s" apiKey
                            apiKey
                        ) 
                        |> decrypt
                        |> Some
                    else
                        authToken 
                        |> decrypt
                        |> Some
                key
                |> Option.bind(fun key ->
                    if  key |> verifyToken then
                        let data = 
                            match cache.TryGet configurationName with
                            None -> 
                                let configuration = DataConfiguration.get configurationName
                                let datasetKey =
                                    sprintf "%s:%s" (configuration.Source.SourceName) (configuration.Source.ProjectName)
                                let rawData =
                                    match Rawdata.list datasetKey with
                                    s when s |> Seq.isEmpty -> 
                                        DataCollector.get configuration.Source |> ignore
                                        Rawdata.list datasetKey
                                    | data -> 
                                        data

                                let transformations = 
                                        Transformations.load configuration.Transformations
                                        |> Array.collect(fun t -> t.Lines)
                                let func = Hobbes.FSharp.Compile.expressions transformations                                                                   
                                (rawData
                                 |> Seq.map(fun (columnName,values) -> 
                                    columnName, values.ToSeq()
                                                 |> Seq.map(fun (i,v) -> Hobbes.Parsing.AST.KeyType.Create i, v)
                                 ) |> DataMatrix.fromTable
                                 |> func).ToJson()
                                |> Cache.store configurationName 
                                
                            | Some cacheRecord -> cacheRecord.Data
                        (200, data) |> Some
                    else
                       None
                ) |> Option.orElseWith(fun () ->
                    eprintfn "Tried to gain access with an invalid key (%A). Token (%s)" key authToken
                    (403, "Unauthorized") |> Some
                ) |> Option.get
        (setStatusCode statusCode
         >=> setBodyFromString body) func ctx

let private helloWorld =
    setStatusCode 200
    >=> setBodyFromString "Hello Lucas"

type private AzureUser = FSharp.Data.JsonProvider<"""{"Email": " lkjljk", "User" : "lkjlkj"}""">

let private tryParseUser (user : string) = 
    match user.Split('|') with
    [|user;_;token|] ->
        let userInfo = 
            user
            |> fromB64
        //github and asure use different formats so lets try and align them
        let user = 
            userInfo.Replace("\"email\"", "\"Email\"").Replace("\"user\"","\"User\"").Trim().Replace(" ", ",").Trim().TrimStart('{').TrimEnd('}')
            |> sprintf "{%s}"
            |> AzureUser.Parse
        Some(user.User, token)
    | _ -> None
   
let private key token =
    let user = 
        token
        |> tryParseUser
        |> Option.bind(fun (user,token) -> 
              let userId = sprintf "org.couchdb.user%%3A%s" user
              match users.TryGet userId with
              None ->
                printfn "Didn't find user. %s" userId
                let userRecord = 
                    sprintf """{
                      "name": "%s",
                      "type": "user",
                      "roles": [],
                      "password": "%s"
                    }""" user token
                userRecord
                |> users.Put userId
                users.Get userId
                |> Some
              | s -> s
        )

    match user with
    None ->
        eprintfn "No user token. Tried with %s" token 
        setStatusCode 403 >=> setBodyFromString "Unauthorized"
    | Some (user) ->
        printfn "Creating api key for %s" user.Name
        let key = createToken user
        setStatusCode 200 >=> setBodyFromString key

let private apiRouter = router {
    not_found_handler (setStatusCode 404 >=> text "Api 404")
    
    getf "/data/%s" data
    get "/helloServer" helloWorld
    getf "/key/%s" key
}

let private appRouter = router {
    forward "" apiRouter
}

let private app = application {
    url (sprintf "http://0.0.0.0:%d/" port)
    use_router appRouter
    memory_cache
    use_gzip
}

run app