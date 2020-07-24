open FSharp.Formatting.Literate
open FSharp.Formatting.Literate.Evaluation


let scriptsDir = ""
let templatesDir = "./templates/"
let defaultTemplate = "template.html"


[<EntryPoint>]
let create args =
    if args.Length = 0 then failwithf "Please input a script file and optionally a template file"
    if args.Length > 2 then failwithf "Please input only script file and optionally a template file"
    let fsi = FsiEvaluator()

    let script = scriptsDir + args.[0]
    let template = if args.Length = 1 then templatesDir + defaultTemplate else templatesDir + args.[1]
    Literate.ConvertScriptFile(script, template, fsiEvaluator = fsi)
    0