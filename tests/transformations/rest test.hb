provider: rest
url: 
    - https://dev.azure.com/kmddk/kmdlogic/_apis/git/repositories/01c03de4-5713-4cad-b3d6-ff14dc4c387e/commits?api-version=6.0&$top=10
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