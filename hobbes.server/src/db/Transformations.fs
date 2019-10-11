module Transformations

let load (transformationIds : #seq<string>) = 
   Database.transformations.FilterByKeys transformationIds