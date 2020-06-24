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
        with member p.Account
                with get() =
                    match p with
                    Flowerpot
                    | UVskole
                    | Nexus                      
                    | EzEnergy 
                    | Gandalf
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
                       "account" : "%s",
                       "project" :"%s"
                   }""" (p.Account) (p.ToString())
               | Git (dataset,p) ->
                   sprintf """{
                       "name" : "git",
                       "project" : "%s",
                       "account" : "%s",
                       "dataset" : "commits"
                   }""" (p.ToString()) (p.Account)
               | Jira _
               | None -> failwith "Don't know what to do"
             member this.Name 
                 with get() = 
                     match this with
                     AzureDevOps p -> "azureDevops." + p.ToString()
                     | Git(ds,p) -> sprintf "git.%s.%s" (p.ToString()) (ds.ToString())
                     | Jira p -> "jira." + p.ToString()
                     | None -> null
    
    
    open Hobbes.Web.RawdataTypes
    let createTransformation name (statements : Hobbes.DSL.Statements list) =
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