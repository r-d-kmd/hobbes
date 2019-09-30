open Saturn
open Giraffe.Core
open Giraffe.ResponseWriters
open Microsoft.AspNetCore.Http
open Hobbes.FSharp.DataStructures
open Database
open Hobbes.Server.Db

let private port = 
    match env "port" with
    null -> 8085
    | p -> int p

let private getBytes (s:string)= 
    System.Text.Encoding.ASCII.GetBytes s

let private toB64 s = 
    System.Convert.ToBase64String s

let private fromB64 s = 
    System.Convert.FromBase64String s
    |> System.Text.Encoding.ASCII.GetString


type private JwtPayload = FSharp.Data.JsonProvider<"""{"name":"some"}""">

let private getSignature personalKey header payload = 
    let hmac = System.Security.Cryptography.HMAC.Create()
    hmac.Key <- 
        (personalKey + System.Environment.GetEnvironmentVariable("KEY_SUFFIX"))
        |> getBytes

    payload
    |> getBytes
    |> toB64
    |> sprintf "%s.%s" header
    |> getBytes
    |> hmac.ComputeHash
    |> toB64

let private createToken payload = 
    let header = 
        """{"alg":"HS256","type":"JWT"}"""
        |> getBytes
        |> toB64
    let user = (JwtPayload.Parse payload).Name |> users.Get
    let signature = getSignature user.DerivedKey header payload
    sprintf "%s.%s.%s" header payload signature

let private verifyToken (token : string) = 
    match token.Split('.') with
    [|header;payload;signature|] ->
        let jwtPayload = JwtPayload.Parse payload
        let user = users.Get jwtPayload.Name
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
                    authToken
                    |> fromB64 
                if  key
                    |> verifyToken then
                    
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
                    200, data
                else
                   eprintfn "Tried to gain access with an invalid. %s" key
                   403, "Unauthorized"
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
        setStatusCode 200 >=> setBodyFromString user.DerivedKey

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