namespace Hobbes.Helpers

open Newtonsoft.Json
module Json = 

    let serialize<'a> (o:'a) = JsonConvert.SerializeObject(o)
    let deserialize<'a> json = JsonConvert.DeserializeObject<'a>(json)