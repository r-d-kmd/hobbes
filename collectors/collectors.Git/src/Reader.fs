namespace Collector.Git
open LibGit2Sharp
open Hobbes.Web.Log

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
            log "Deleting old repo"
            System.IO.Directory.Delete(repoDir,true)

        let options = CloneOptions()
        options.CredentialsProvider <- ca
        logf "Cloning repo %s into %s" url repoDir
        Repository.Clone(url, repoDir, options) |> ignore
        logf "Done cloning repo %s into %s" url repoDir
    
    type Commit = {
        Time : System.DateTime
        Message : string
        Author : string
    }

    let commits url : Commit list = 
        try
            let repoDir = repoDir url
            logf "opening repo at %s" repoDir
            use repo = new Repository(repoDir)
            logf "Repo opened"
            (*repo.Commits //avoid lazy evaluation since that might cause a memory overwrite issue
            |> List.ofSeq 
            |> List.map(fun commit ->
                logf "reading commit: %s" commit.Id.Sha
                {
                    Time = commit.Author.When.ToLocalTime().DateTime
                    Message = commit.MessageShort
                    Author = commit.Author.Email
                }
            )*)
            []
        with e ->
            errorf e.StackTrace "Failed when reading commits. %s" e.Message
            []
           
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
        |> List.ofSeq
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
        
        