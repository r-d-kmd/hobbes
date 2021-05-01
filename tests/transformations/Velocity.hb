provider: odata
url: https://analytics.dev.azure.com/kmddk/flowerpot/_odata/v2.0/WorkItemRevisions?
expand: Iteration&%24select=State,ChangedDate,WorkITemId%2CWorkItemType%2CState%2CStateCategory%2CIteration
filter: Iteration%2FStartDate%20gt%202019-01-01Z
user: $AZURE_DEVOPS_PAT
pwd: $AZURE_DEVOPS_PAT
meta:
  category: workitems
  name: flowerpot


!## Velocity
A simple measure of how much we deliver each sprint. The graph shows a moving average of how many user stories are completed each sprint.
## Reading the graph
If we are improving there will be an upwards trend. The two lines represent a shorter and a longer persepctive.
The trend of the "long" is a stronger signal however if the short has recently changed direction, to the opposite of the long trand, this might be a sign that the long trend will change in the near future
!#


only (WorkItemType = 'User Story')
group by "Iteration.IterationName" WorkItemId -> maxby ChangedDate
rename column "Iteration.IterationLevel3" "Sprint Name"
create column SprintNumber (int (regex ["Sprint Name"] /[Ss][Pp][Rr][Ii][Nn][Tt] [^\\d]*([\\d]+).*/ [$1]))
rename column State "DetailedState"
create column State (if [StateCategory = 'Completed' || StateCategory = 'Resolved' || StateCategory = 'Remove'] {'Done'} else { if ["StateCategory" = 'InProgress'] {'Doing'} else {'Todo'} })
only !(SprintNumber > 45)
pivot [SprintNumber] [State] -> count [SprintNumber]
sort by column SprintNumber
create column "Velocity 3" (moving mean 3 [Done])
create column "Velocity 7" (moving mean 7 [Done])
slice columns "Velocity 3" "Velocity 7"


!### Method
We can improve this number by either being more efficient at want we do or by creating smaller user stories. Both are something that we want to do.
Since there are multiple ways to impact the graph we won't know to what extend they both influence the change but only that an upwards trend is positive.
!#