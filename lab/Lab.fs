open System
open Hobbes.Parsing
open Hobbes.DSL
open FSharp.Data

let inline debug(isDebugging) = 
    if isDebugging && not(Diagnostics.Debugger.IsAttached) then
        printfn "Please attach a debugger, PID: %d" (Diagnostics.Process.GetCurrentProcess().Id)
        while not(Diagnostics.Debugger.IsAttached) do
              Threading.Thread.Sleep(100)
        Diagnostics.Debugger.Break()

let stopwatch = Diagnostics.Stopwatch()
let compile stmt =
    stmt, Compile.expressions [stmt]
let execute (data : Hobbes.DataStructures.IDataMatrix) = 
    if stopwatch.IsRunning |> not then stopwatch.Start()
    List.fold(fun table (source,stmt) ->
        let s = stopwatch.ElapsedMilliseconds
        let res = stmt table
        let time = stopwatch.ElapsedMilliseconds - s
        printfn "(%dms):\t %s" time source
        res
    ) data

let modelling (data : Hobbes.DataStructures.IDataMatrix) = 
    let start = stopwatch.ElapsedMilliseconds
    let dataModelingTransformation =
        let state = !> "State"
        [
            slice columns ["Iteration.IterationLevel4";"WorkItemId";"ChangedDate";"WorkItemType";"State";]
            dense rows
            rename "Iteration.IterationLevel4" "Sprint" 
            create (column "ProgressState") (If ((state == "Ready") .|| (state == "Ready for estimate") .|| (state == "New"))
                                                (Then !!> "Todo")
                                                (Else 
                                                    (If ((state == "In sprint") .|| (state == "Active" ))
                                                        (Then "Doing")
                                                        (Else "Done"))))

            slice columns ["Sprint";"WorkItemId";"ProgressState";"ChangedDate";"WorkItemType"]
            rename "ProgressState" "State" 
            
        ] |> List.map (string >> compile)

    let modelledData = 
        dataModelingTransformation 
        |> execute data

    let userStoriesCreate = 
        [
            only (!> "WorkItemType" == "User Story" .&& (!> "State" == "Todo"))
            group by ["WorkItemId"] => minby !> "ChangedDate"
        ] |> List.map (string >> compile)

    let userStoriesDone = 
        [
            only (!> "WorkItemType" == "User Story" .&& (!> "State" == "Done")) 
            group by ["WorkItemId"] => maxby !> "ChangedDate"
        ] |> List.map (string >> compile)

    let userStoriesDoneData = 
       userStoriesDone
       |> execute modelledData
       
    let userStoriesCreateData = 
       userStoriesCreate
       |> execute modelledData

    let combinedData = 
        userStoriesCreateData.Combine  userStoriesDoneData
    printfn "Data modelled in %dms" (stopwatch.ElapsedMilliseconds - start)
    combinedData
        
[<EntryPoint>]
let main args =
    
    debug(args.Length > 0 && args.[0] = "debug")
    let projectName = "gandalf"
    let dataVersion =
        "small"
        //"medium"
        //"full"
    stopwatch.Start()
    async {

        let! data = 
            if dataVersion = "full" then 
                Azure.Reader.read stopwatch projectName modelling
            else
                async {
                    let x = dataVersion
                            |> sprintf "data\\azure_%s.json"
                            |> IO.File.ReadAllText 
                            |> JsonValue.Parse
                    return  x.["value"].AsArray()
                            |> Azure.Reader.readJson Map.empty
                            |> snd
                            |> Seq.map(fun (columnName, columnValues) -> 
                                                columnName, columnValues 
                                                            |> Seq.mapi(fun index value -> AST.KeyType.Create index, value))
                            |> Hobbes.DataStructures.DataMatrix.fromTable
                            |> modelling
                }

        let transformation =
            [
                pivot (!> "Sprint") (!> "State") (AST.Count) (!> "WorkItemId")
                create (column "Sprint") Keys
                sort by "Sprint"
                create (column "Burn Up")  (expanding AST.Sum !> "Done")
                create (column "Created")  (expanding AST.Sum !> "Todo") 
                create (column "Outstanding scope") ((!> "Created"  - !> "Burn Up"))
            ] |> List.map (string >> compile)
        
        let result = 
            transformation
            |> execute data
        
        printfn "Total transformation time: %dms" stopwatch.ElapsedMilliseconds
        IO.File.WriteAllText (sprintf "results/%s_result.json" projectName, result.AsJson())
    } |> Async.RunSynchronously
    0
