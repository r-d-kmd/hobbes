namespace Collector.Git
open LibGit2Sharp

module Reader =

    let inline private (<+>) (delim : string) (lines : seq<string>) = 
        System.String.Join(delim,lines)

    let private hash (input : string) =
            use md5Hash = System.Security.Cryptography.MD5.Create()
            let data = md5Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input))
            let sBuilder = System.Text.StringBuilder()
            (data
            |> Seq.fold(fun (sBuilder : System.Text.StringBuilder) d ->
                    sBuilder.Append(d.ToString("x2"))
            ) sBuilder).ToString()
            
    let repoDir url = 
        url 
        |> hash 
        |> sprintf "./repositories/%s" 
        |> System.IO.Path.GetFullPath

    let sync user pwd url = 
        
        let repoDir =  
            repoDir url

        let ca = 
            Handlers.CredentialsHandler(fun _ _ _ -> 
                UsernamePasswordCredentials(Username = user, Password = pwd) :> Credentials
            )
        
        if System.IO.Directory.Exists repoDir then
            System.IO.Directory.Delete(repoDir,true)

        let options = CloneOptions()
        options.CredentialsProvider <- ca
        Repository.Clone(url, repoDir, options) |> ignore

    let commits url = 
        let repoDir = repoDir url
        use repo = new Repository(repoDir)

        let rows = 
            repo.Commits
            |> Seq.map(fun commit ->
                sprintf "%s,%s" commit.MessageShort (commit.Author.When.ToLocalTime().ToString())
            )
        "message,timestamp\n" +     
        "\n" <+> rows

    let branches url =
        let repoDir = repoDir url
        use repo = new Repository(repoDir)
        let branches = repo.Branches

        let rows = 
            branches
            |> Seq.map(fun branch -> 
                let commits = 
                    branch.Commits
                    |> Seq.sortBy(fun c -> c.Author.When)
                let branchLifeTimeInHours = 
                    ((commits |> Seq.maxBy(fun c -> c.Author.When)).Author.When
                     - (commits |> Seq.head).Author.When).TotalMinutes / 60.
                     |> int
                let treeName,branchName = 
                    match branch.FriendlyName.Split("/") with
                    [|a|] -> "",a
                    | [|treeName;branchName|] -> treeName,branchName
                    | tags -> 
                        let tags = tags |> Array.tail
                        tags |> Array.head, System.String.Join("/", tags |> Array.tail)
               
                let row = sprintf "%s,%s,%d,%d,%d" treeName branchName branchLifeTimeInHours (branchLifeTimeInHours/24) (branchLifeTimeInHours/24/30)
                row
            )
        "tree,branch,life time in hours, lifetime in days, life time in months\n" + 
        "\n" <+> rows
        