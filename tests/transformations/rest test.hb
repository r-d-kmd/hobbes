provider: rest
url: 
    - https://dev.azure.com/kmddk/kmdlogic/_apis/git/repositories/01c03de4-5713-4cad-b3d6-ff14dc4c387e/commits?api-version=6.0&$top=10000
    - https://dev.azure.com/kmddk/kmdlogic/_apis/git/repositories/8622dba3-3a68-4a16-964a-03c42fd6033a/commits?api-version=6.0&$top=10000
    - https://dev.azure.com/kmddk/kmdlogic/_apis/git/repositories/1b0538f6-d148-410a-9f20-19503f58f6b3/commits?api-version=6.0&$top=10000  
    - https://dev.azure.com/kmddk/kmdlogic/_apis/git/repositories/a9ab1f83-75a6-485a-a661-51c9bff61e02/commits?api-version=6.0&$top=10000
    - https://dev.azure.com/kmddk/kmdlogic/_apis/git/repositories/cee4927a-b744-4eeb-a53d-667fa566d40b/commits?api-version=6.0&$top=10000
    - https://dev.azure.com/kmddk/kmdlogic/_apis/git/repositories/c46d363e-a776-4eb7-8b1a-729af3abb12e/commits?api-version=6.0&$top=10000
    - https://dev.azure.com/kmddk/kmdlogic/_apis/git/repositories/7e718294-4769-4b69-a347-76d4a0f9a068/commits?api-version=6.0&$top=10000
    - https://dev.azure.com/kmddk/kmdlogic/_apis/git/repositories/eeedb2a2-b5ff-4e62-8b23-81508847fb3c/commits?api-version=6.0&$top=10000
    - https://dev.azure.com/kmddk/kmdlogic/_apis/git/repositories/8f0ac062-6c34-4ed5-bbdd-a0b40913bf95/commits?api-version=6.0&$top=10000
    - https://dev.azure.com/kmddk/kmdlogic/_apis/git/repositories/4c681043-e5bb-4741-871d-acf88efa8316/commits?api-version=6.0&$top=10000
    - https://dev.azure.com/kmddk/kmdlogic/_apis/git/repositories/f41894ee-c71a-469c-8457-c2e08dbd90d1/commits?api-version=6.0&$top=10000
    - https://dev.azure.com/kmddk/kmdlogic/_apis/git/repositories/a2e81ac6-7a1a-4f6e-b9bd-ca44864a3f7b/commits?api-version=6.0&$top=10000
    - https://dev.azure.com/kmddk/kmdlogic/_apis/git/repositories/2191c6f8-8dda-4cbc-b9e0-fbdc5bfabb66/commits?api-version=6.0&$top=10000
    - https://dev.azure.com/kmddk/kmdlogic/_apis/git/repositories/01c03de4-5713-4cad-b3d6-ff14dc4c387e/commits?api-version=6.0&$top=10000
    - https://dev.azure.com/kmddk/kmdlogic/_apis/git/repositories/fbf1b24e-c1b9-48f4-bfbc-ff2dbf46220b/commits?api-version=6.0&$top=10000
user: $AZURE_DEVOPS_PAT 
pwd: $AZURE_DEVOPS_PAT
values: value


!## Commit Frequency
Research shows that products that have a high commit frequency are more likely do perform well from a business perspective. 
Having high commit frequency makes the feedback loop between individual developers shorter. 
However for that to be true it is important to understand that it is commits to trunk that are essential not commits to a particular branch
## Reading the graph
If we are improving there will be an upwards trend. The two lines represent a shorter and a longer persepctive. 
The trend of the "long" is a stronger signal however if the short has recently changed direction, to the opposite of the long trand, this might be a sign that the long trend will change in the near future
!#