namespace Workbench

[<AutoOpen>]
module Types = 

    type GitDataSet =
         Branches
         | Commits
         with override this.ToString() = 
                  match this with
                  Branches -> "branches"
                  | Commits -> "commits"

    [<RequireQualifiedAccess>]
    type Project =
        Flowerpot
        | UVskole
        | Nexus 
        | Delta 
        | EzEnergy 
        | Gandalf
        | Momentum
        with override this.ToString() = 
               match this with
               Flowerpot -> "Flowerpot"
               | UVskole -> "UVskole"
               | Nexus  -> "Nexus "
               | Delta  -> "Delta "
               | EzEnergy  -> "EzEnergy "
               | Gandalf -> "Gandalf"
               | Momentum -> "Momentum"

    [<RequireQualifiedAccess>]
    type Source = 
        AzureDevOps of Project
        | Git of GitDataSet * Project
        | Jira of Project
        | None
        with override this.ToString() = 
               match this with
               AzureDevOps p ->
                   sprintf """{
                       "name" : "azure devops",
                       "project" : %s
                   }""" (p.ToString())
               | Git (dataset,p) ->
                   sprintf """{
                       "name" : "git",
                       "project" : %s,
                       "dataset" : %s
                   }""" (p.ToString()) (dataset.ToString())
               | Jira _
               | None -> failwith "Don't know what to do"
             member this.Name 
                 with get() = 
                     match this with
                     AzureDevOps p -> "azureDevops." + p.ToString()
                     | Git(ds,p) -> sprintf "git.%s.%s" (p.ToString()) (ds.ToString())
                     | Jira p -> "jira." + p.ToString()
                     | None -> null
    

    type Transformation = 
        {
            Name : string
            Statements : Hobbes.DSL.Statements list
            Description : string
        } with static member Empty 
                 with get() = {Name = null; Statements = []; Description = null}
               static member Create name statements =
                   {
                       Name = name
                       Statements = statements
                       Description = null
                   }
               override this.ToString() =
                    let parse stmt =
                        let stmt = stmt |> string
                        Hobbes.Parsing.Parser.parse [stmt]
                        |> Seq.exactlyOne

                    this.Statements
                    |> List.map parse
                    |> ignore
                    
                    System.String.Join(",",
                        this.Statements
                        |> List.map (fun stmt ->
                           (stmt |> string).Replace("\\","\\\\\\\\").Replace("\"", "\\\"") |> sprintf "\n  %A"
                        )
                    ) |> sprintf "[%s\n]"
                    |> sprintf """{
                        "_id" : "%s",
                        "description" : %s,
                        "lines" : %s
                    }
                    """ this.Name (if this.Description |> isNull then "" else this.Description)
                 
    type Configuration =
        {
            Name : string
            Source : Source
            Transformations : Transformation list
        } with static member Empty 
                  with get() =
                        {
                            Name = null
                            Source = Source.None
                            Transformations  = []
                        }
               override this.ToString() = 
                    sprintf """{
                        "name" : "%s",
                        "source" : %s,
                        "transformations" : [%s]
                    }""" this.Name 
                         (this.Source.ToString()) 
                         (System.String.Join(",",this.Transformations |> Seq.map (fun t -> t.Name |> sprintf "%A")))

    let mutable private configurations : Map<string,Configuration> = Map.empty
    let addConfiguration (source : Source ) name transformations =
      configurations <- 
              let name = source.Name + "." + name
              match configurations |> Map.tryFind name with
              Some _ -> failwithf "There's already a configuration called %s" name
              | None ->
                configurations |> Map.add name {
                                                    Name = name
                                                    Source = source
                                                    Transformations = 
                                                        transformations
                                               }
    let allConfigurations() = 
        configurations
        |> Map.toSeq
        |> Seq.map snd 
    
    let allTransformations() = 
        allConfigurations()
        |> Seq.collect(fun c -> c.Transformations)
        |> Seq.distinctBy(fun t -> t.Name)