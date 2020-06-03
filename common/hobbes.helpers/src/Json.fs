namespace Hobbes.Helpers

open Newtonsoft.Json
open Microsoft.FSharp.Reflection
open System
open System.IO

module Json = 

    type DuConverter() =
        inherit JsonConverter()

        override __.WriteJson(writer, value, serializer) =
            let unionType = value.GetType()
            let unionCases = FSharpType.GetUnionCases(unionType)
            let case, fields = FSharpValue.GetUnionFields(value, unionType)
            let allCasesHaveValues = unionCases |> Seq.forall (fun c -> c.GetFields() |> Seq.length > 0)

            let distinctCases = unionCases |> Seq.distinctBy (fun c->c.GetFields() |> Seq.map (fun f-> f.DeclaringType))
            let hasAmbigious = (distinctCases |> Seq.length) <> (unionCases |> Seq.length)

            let allSingle = unionCases |> Seq.forall (fun c -> c.GetFields() |> Seq.length = 1)

            match allSingle,fields with
            //simplies case no parameters - just like an enumeration
            | _,[||] -> writer.WriteRawValue(sprintf "\"%s\"" case.Name)
            //all single values - discriminate between record types - so we just serialize the record
            | true,[| singleValue |] -> serializer.Serialize(writer,singleValue)
            //diferent types in same discriminated union - write the case and the items as tuples
            | false,values ->
                writer.WriteStartObject()
                writer.WritePropertyName "Case"
                writer.WriteRawValue(sprintf "\"%s\"" case.Name)
                let valuesCount = Seq.length values
                for i in 1 .. valuesCount do
                    let itemName = sprintf "Item%i" i
                    writer.WritePropertyName itemName
                    serializer.Serialize(writer,values.[i-1])
                writer.WriteEndObject()
            | _,_ -> failwith "Handle this new case"




        override __.ReadJson(reader, destinationType, existingValue, serializer) =
            let parts =
                if reader.TokenType <> JsonToken.StartObject then [| (JsonToken.Undefined, obj()), (reader.TokenType, reader.Value) |]
                else
                    seq {
                        yield! reader |> Seq.unfold (fun reader ->
                                             if reader.Read() then Some((reader.TokenType, reader.Value), reader)
                                             else None)
                    }
                    |> Seq.takeWhile(fun (token, _) -> token <> JsonToken.EndObject)
                    |> Seq.pairwise
                    |> Seq.mapi (fun id value -> id, value)
                    |> Seq.filter (fun (id, _) -> id % 2 = 0)
                    |> Seq.map snd
                    |> Seq.toArray

            //get simplified key value collection
            let fieldsValues =
                parts
                    |> Seq.map (fun ((_, fieldName), (fieldType,fieldValue)) -> fieldName,fieldType,fieldValue)
                    |> Seq.toArray
            //all cases of the targe discriminated union
            let unionCases = FSharpType.GetUnionCases(destinationType)

            //the first simple case - this DU contains just simple values - as enum - get the value
            let _,_,firstFieldValue = fieldsValues.[0]

            let fieldsCount = fieldsValues |> Seq.length

            let valuesOnly = fieldsValues |> Seq.skip 1 |> Seq.map (fun (_,_,v) -> v) |> Array.ofSeq

            let foundDirectCase = unionCases |> Seq.tryFind (fun uc -> uc.Name = (firstFieldValue.ToString()))

            let jsonToValue valueType value =
                match valueType with
                                    | JsonToken.Date ->
                                        let dateTimeValue = Convert.ToDateTime(value :> Object)
                                        dateTimeValue.ToString("o")
                                    | _ -> value.ToString()

            match foundDirectCase, fieldsCount with
                //simpliest case - just like an enum
                | Some case, 1 -> FSharpValue.MakeUnion(case,[||])
                //case is specified - just create the case with the values as parameters
                | Some case, n -> FSharpValue.MakeUnion(case,valuesOnly)
                //case not specified - look up the record type which suites the best
                | None, _ ->
                    //this is the second case - this disc union is not of simple value - it may be records or multiple values
                    let reconstructedJson = (Seq.fold (fun acc (name,valueType,value) -> acc + String.Format("\t\"{0}\":\"{1}\",\n",name,(jsonToValue valueType value))) "{\n" fieldsValues) + "}"

                    //if it is a record lets try to find the case by looking at the present fields
                    let implicitCase = unionCases |> Seq.tryPick (fun uc ->
                        //if the case of the discriminated union is a record then this case will contain just one field which will be the record
                        let ucDef = uc.GetFields() |> Seq.head
                        //we need the get the record type and look at the fields
                        let recordType = ucDef.PropertyType
                        let recordFields = recordType.GetProperties()
                        let matched = fieldsValues |> Seq.forall ( fun (fieldName,_,fieldValue) ->
                            recordFields |> Array.exists(fun f-> f.Name = (fieldName :?> string))
                        )    
                        //if we have found a match onthe record let's keep the union case and type of the record
                        match matched with
                            | true -> Some (uc,recordType)
                            | false -> None
                    )

                    match implicitCase with
                        | Some (case,recordType) ->
                            use stringReader = new StringReader(reconstructedJson)
                            use jsonReader = new JsonTextReader(stringReader)
                            //creating the record - Newtonsoft.Json can handle that already
                            let unionCaseValue = serializer.Deserialize(jsonReader,recordType)
                            //convert the record to the parent discrimianted union
                            let parentDUValue = FSharpValue.MakeUnion(case,[|unionCaseValue|])
                            parentDUValue
                        | None -> failwith "can't find such disc union type"

        override __.CanConvert(objectType) =
            FSharpType.IsUnion objectType &&
            //it seems that both option and list are implemented using discriminated unions, so we tell Newtonsoft.Json to ignore them and use different serializer
            not (objectType.IsGenericType  && objectType.GetGenericTypeDefinition() = typedefof<list<_>>) &&
            not (objectType.IsGenericType  && objectType.GetGenericTypeDefinition() = typedefof<option<_>>) &&
            not (FSharpType.IsRecord objectType)

    let private settings = JsonSerializerSettings()
    settings.Converters.Add(DuConverter())
    let serialize<'a> (o:'a) = JsonConvert.SerializeObject(o,settings)
    let deserialize<'a> json = JsonConvert.DeserializeObject<'a>(json,settings)