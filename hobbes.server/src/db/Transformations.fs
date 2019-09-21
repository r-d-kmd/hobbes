module Transformations
open FSharp.Data

let load (transformationIds : #seq<string>) = 
   Database.transformations.List transformationIds