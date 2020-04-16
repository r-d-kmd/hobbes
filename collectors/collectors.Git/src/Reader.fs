namespace Collector.Git
open LibGit2Sharp

module Reader =

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
            Hobbes.Web.Log.log "Deleting old repo"
            System.IO.Directory.Delete(repoDir,true)

        let options = CloneOptions()
        options.CredentialsProvider <- ca
        Hobbes.Web.Log.logf "Cloning repo %s into %s" url repoDir
        Repository.Clone(url, repoDir, options) |> ignore
        Hobbes.Web.Log.logf "Done cloning repo %s into %s" url repoDir

    let commits url = 
        let repoDir = repoDir url
        use repo = new Repository(repoDir)
        repo.Commits
           
    type BranchData = {
        TreeName : string
        Name : string
        LifeTimeInHours : int
    }

    let branches url =
        let repoDir = repoDir url
        use repo = new Repository(repoDir)
        let branches = repo.Branches
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
           
            {
                Name = branchName
                TreeName = treeName
                LifeTimeInHours = branchLifeTimeInHours
            }
        )
        
        