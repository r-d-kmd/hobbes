module DataCollector

let get source datasetName = 
    seq {
       yield "some column", Seq.empty
    }