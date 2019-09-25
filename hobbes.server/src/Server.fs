open Saturn
open Giraffe.Core
open Giraffe.ResponseWriters
open Microsoft.AspNetCore.Http
open Hobbes.FSharp.DataStructures
open FSharp.Data
open Database

let private port = 
    match System.Environment.GetEnvironmentVariable("port") with
    null -> 8085
    | p -> int p

let private getBytes (s:string)= 
    System.Text.Encoding.ASCII.GetBytes s

let private toB64 s = 
    System.Convert.ToBase64String s

let private fromB64 s = 
    System.Convert.FromBase64String s
    |> System.Text.Encoding.ASCII.GetString


type JwtPayload = FSharp.Data.JsonProvider<"""{"name":"some"}""">

let private getSignature personalKey header payload = 
    let hmac = System.Security.Cryptography.HMAC.Create()
    hmac.Key <- 
        (personalKey + System.Environment.GetEnvironmentVariable("key_suffix"))
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

let enumeratorMap f (e : System.Collections.IEnumerator) = //Shouldn't be here
    let rec aux acc =
        match e.MoveNext() with
        true -> aux (f e.Current :: acc)
        | false -> acc
    aux []
      

let private getData configurationName =
    
    fun func (ctx : HttpContext) ->
        let statusCode, body =  
            match ctx.TryGetRequestHeader "Authorization" with
            None ->    
                eprintfn "Tried to gain access without a key"
                403, "Unauthorized"
            | Some authToken ->
                (*let key =
                    authToken
                    |> fromB64 
                if  key
                    |> verifyToken then*)
                    
                    let data = 
                        match cache.TryGet configurationName with
                        None -> 
                            let configuration = DataConfiguration.get configurationName
                            
                            let rawData =
                                match rawdata.TryGet (sprintf "%s:%s" configuration.Source configuration.Dataset) with
                                None -> 
                                    (DataCollector.get configuration.Source configuration.Dataset
                                    |> DataMatrix.fromTable).AsJson()
                                    |> Rawdata.store (sprintf "%s:%s" configuration.Source configuration.Dataset)
                                | Some rawdataRecord -> rawdataRecord.Data 

                            let transformations = 
                                    Transformations.load configuration.Transformations
                                    |> Array.collect(fun t -> t.Lines)
                            let func = Hobbes.FSharp.Compile.expressions transformations                                                                   
                            ((rawData
                             |> DataSet.Parse).JsonValue.Properties()
                             |> Seq.map (fun (c, rs) -> (c, 
                                                         rs.GetEnumerator()
                                                         |> enumeratorMap (fun v -> (Hobbes.Parsing.AST.KeyType.Create v, v))
                                                         )
                                        )
                             |> DataMatrix.fromTable
                             |> func).AsJson()
                             |> Cache.store configurationName 
                            
                        | Some cacheRecord -> cacheRecord.Data
                    200, data
                (*else
                   eprintfn "Tried to gain access with an invalid. %s" key
                   403, "Unauthorized"*)
        (setStatusCode statusCode
         >=> setBodyFromString body) func ctx

let GetHelloWorld =
    setStatusCode 200
    >=> setBodyFromString "Hello Lucas"

type AzureUser = FSharp.Data.JsonProvider<"""{"Email": " lkjljk", "User" : "lkjlkj"}""">

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
   
let getKey token =
    let user = 
        token
        |> tryParseUser
        |> Option.bind(fun (user,token) -> 
              match users.TryGet user with
              None ->
                let userId =
                    sprintf "%s" user
                sprintf """{
                  "_id": "org.couchdb.user:%s",
                  "_rev": "1-39b7182af5f4dc7a72d1782d808663b1",
                  "name": "%s",
                  "type": "user",
                  "roles": []
                  "password": "%s"
                }""" userId user token
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

let getTest =
    let resp () = Http.RequestString("http://db:5984/", headers = ["Authorization", "YWRtaW46cGFzc3dvcmQ="])
    setStatusCode 200
    >=> setBodyFromString (resp() )
    

let apiRouter = router {
    not_found_handler (setStatusCode 404 >=> text "Api 404")
    
    getf "/data/%s" getData
    get "/HelloServer" GetHelloWorld
    getf "/key/%s" getKey
    get "/test" getTest
}

let appRouter = router {
    forward "" apiRouter
}

let app = application {
    url (sprintf "http://0.0.0.0:%d/" port)
    use_router appRouter
    memory_cache
    use_gzip
}

run app