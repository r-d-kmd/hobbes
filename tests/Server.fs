module Server

open System
open Xunit
    
[<Fact>]
let ``encrypt round trip``() =

    let encrypt (plainText : string) = 
        use rijAlg = new RijndaelManaged()
        rijAlg.Key <- cryptoKey
        rijAlg.IV <- initializationVector
        let encryptor = rijAlg.CreateEncryptor(rijAlg.Key, rijAlg.IV)
        use msEncrypt = new MemoryStream()
        use csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write)
        use swEncrypt = new StreamWriter(csEncrypt)
        swEncrypt.Write(plainText)
        msEncrypt.ToArray()
        |> toB64

    let decrypt (base64Text : string) = 
        let cipherText = base64Text |> System.Convert.FromBase64String
        use rijAlg = new RijndaelManaged()
        rijAlg.Key <- cryptoKey
        rijAlg.IV <- initializationVector
        let decryptor = rijAlg.CreateDecryptor(rijAlg.Key, rijAlg.IV)
        use msDecrypt = new MemoryStream(cipherText)
        use csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read)
        use srDecrypt = new StreamReader(csDecrypt)
        srDecrypt.ReadToEnd()

    let text = "foo bar"
    Assert.Equal(text, text |> encrypt |> decrypt)