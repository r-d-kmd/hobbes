provider: rest
url: 
    - https://dev.azure.com/kmddk/kmdlogic/_apis/git/repositories/01c03de4-5713-4cad-b3d6-ff14dc4c387e/commits?api-version=6.0&$top=10
user: $AZURE_DEVOPS_PAT 
pwd: $AZURE_DEVOPS_PAT
values: value


!## Rest provider
**url** is an array and will concatenate the response from all the urls provided
**user** is optional and if provided can be either a hard coded value or reference an env var
**pwd** an optional password
**values** the name of the json property that holds the array of data to use. If the response is an array omit this property
!#