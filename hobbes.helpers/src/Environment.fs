namespace Hobbes.Helpers

[<AutoOpen>]
module Environment = 
    let env name defaultValue = 
            match System.Environment.GetEnvironmentVariable name with
            null -> defaultValue
            | v -> v

    