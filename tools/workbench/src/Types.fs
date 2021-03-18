namespace Workbench
open Hobbes.Web.RawdataTypes
open Thoth.Json.Net

[<AutoOpen>]
module Types = 

    type GitDataSet =
         Branches
         | Commits
         with override this.ToString() = 
                  match this with
                  Branches -> "branches"
                  | Commits -> "commits"
    type Collection = 
        Test
        | Development
        | Production
        | All
        with override x.ToString() = 
                match x with
                Test -> "Test"
                | Development -> "Development"
                | Production -> "Production"
                | All -> "All"

    [<RequireQualifiedAccess>]
    type Project =
        Flowerpot
        | UVskole
        | Nexus 
        | Delta 
        | EzEnergy 
        | Gandalf
        | Momentum
        | Logic
        with member p.Account
                with get() =
                    match p with
                    Flowerpot
                    | UVskole
                    | Nexus                      
                    | EzEnergy 
                    | Gandalf
                    | Logic
                    | Momentum -> "kmddk"
                    | Delta -> "time-payroll-kmddk"
             override this.ToString() = 
               match this with
               Flowerpot -> "Flowerpot"
               | UVskole -> "UVskole"
               | Nexus  -> "Nexus"
               | Delta  -> "Delta"
               | EzEnergy  -> "EzEnergy"
               | Gandalf -> "Gandalf"
               | Momentum -> "Momentum"
               | Logic -> "KMDLoGIC"
    type Join =
        {
            Left : string
            Right : string
            Field : string
        }

    [<RequireQualifiedAccess>]
    type Source = 
        AzureDevOps of Project
        | Git of GitDataSet * Project
        | Jira of Project
        | Merge of string list
        | Join of Join
        | Local of id:string * columns: string list * rows: obj list list
        | None
        with override this.ToString() = 
               match this with
               AzureDevOps p ->
                   sprintf """{
                       "provider" : "azure devops",
                       "id" : "%s",
                       "account" : "%s",
                       "project" :"%s"
                   }""" (this.Name) (p.Account) (p.ToString()) 
               | Git (dataset,p) ->
                   sprintf """{
                       "provider" : "git",
                       "id" : "%s",
                       "project" : "%s",
                       "account" : "%s",
                       "dataset" : "commits"
                   }""" (this.Name) (p.ToString()) (p.Account)
               | Merge ids ->
                    sprintf """{
                        "id" : "%s",
                        "provider" : "merge",
                        "datasets" : [%s]
                    }""" this.Name (System.String.Join(",",ids |> List.map (sprintf "%A")))
               | Join join ->
                   sprintf """{
                       "provider" : "join",
                       "id" : "%s"
                        "left": "%s",
                        "right" : "%s",
                        "field" : "%s"
                    }""" this.Name join.Left join.Right join.Field
               | Local (id,columns,rows) ->
                    Encode.object
                        [
                            "provider", Encode.string "localdata"
                            "id",Encode.string id
                            "columns", columns |> List.map Encode.string |> Encode.list
                            "rows", 
                                    rows
                                    |> List.map(fun row ->
                                        row
                                        |> List.map(fun v ->
                                            match v with
                                            :? int as i -> 
                                               Encode.int i
                                            | :? float as f -> 
                                               Encode.float f
                                            | :? string as s -> 
                                                Encode.string s
                                            | v -> v |> string |> Encode.string
                                        ) |> Encode.list
                                    ) |> Encode.list
                        ]
                    |> Encode.toString 0
               | Jira _
               | None -> failwith "Don't know what to do"
             member this.Name 
                 with get() = 
                     match this with
                     AzureDevOps p -> "azureDevops." + p.ToString()
                     | Git(ds,p) -> sprintf "git.%s.%s" (p.ToString()) (ds.ToString())
                     | Jira p -> "jira." + p.ToString()
                     | Merge ids -> "merge." + (System.String.Join("+",ids))
                     | Join join -> sprintf "join.%s.%s.%s" join.Left join.Right join.Field
                     | Local(id,columns,_) -> System.String.Join(".", "local"::id::columns)
                     | None -> null
    
    
    open Hobbes.Web.RawdataTypes
    let parse stmt =
        let stmt = stmt |> string
        Hobbes.Parsing.StatementParser.parse stmt
        |> Seq.exactlyOne
    let createTransformation name (statements : Hobbes.DSL.Statements list) =
       //verify the validity of the statements
       statements
       |> List.iter (parse >> ignore)

       {
           Name = name
           Statements = 
               statements |> List.map string
           Description = null
       }
               
    type Configuration =
        {
            Name : string
            Transformations : Transformation list
            Source : Source
        } with static member Empty 
                  with get() =
                        {
                            Name = null
                            Transformations  = []
                            Source = Source.None
                        }
               
    let mutable private configurations : Map<Collection,Map<string,Configuration>> = Map.empty
    let rec addConfiguration (collection : Collection) (source : Source ) name transformations =
        let add collection =
            let collectionConfigurations = 
                match configurations |> Map.tryFind collection with
                None -> Map.empty
                | Some c -> c

            let name = source.Name + "." + name

            let updateConfToCacheKey name =
                let conf = collectionConfigurations
                           |> Map.find name
                let sourceId = conf.Source.ToString()
                               |> keyFromSourceDoc
                let trans = 
                    conf.Transformations
                    |> List.map (fun t -> t.Name)
                System.String.Join(":",sourceId::trans)

            let source = match source with
                         | Source.Merge names -> names
                                                 |> List.map updateConfToCacheKey
                                                 |> Source.Merge 
                         | Source.Join {Field = f; Left = l; Right = r} -> {
                                                                                Field = f
                                                                                Left  = updateConfToCacheKey l
                                                                                Right = updateConfToCacheKey r
                                                                           }
                                                                           |> Source.Join
                         | s -> s
                         
            match collectionConfigurations |> Map.tryFind name with
            Some _ -> eprintf "There's already a configuration called %s" name
                      configurations
            | None ->
                configurations.Add(collection,
                                   collectionConfigurations 
                                   |> Map.add name {
                                          Name = name
                                          Source = source
                                          Transformations = 
                                              transformations
                                     })
        match collection with
        All ->
            [
                Test
                Development
                Production
            ] |> List.iter(fun col ->
                  addConfiguration col (source : Source ) name transformations
            )
        | Test ->
            configurations <- add Test
        | Production ->
            configurations <- add collection
        | _ -> 
            configurations <- add collection
                
    let rec allConfigurations collection =
        match collection with
        All ->
            [
                Test
                Development
                Production
            ] |> Seq.collect allConfigurations
        | c -> 
            match configurations |> Map.tryFind c with
            None -> failwithf "Couldn't find %s in %A" (string c) (configurations |> Map.toList |> List.map fst)
            | Some c ->
                c
                |> Map.toSeq
                |> Seq.map snd 
    
    let allTransformations = 
        allConfigurations
        >> Seq.collect(fun c -> c.Transformations)
        >> Seq.distinctBy(fun t -> t.Name)