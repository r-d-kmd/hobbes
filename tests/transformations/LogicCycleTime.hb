provider: odata
url: https://analytics.dev.azure.com/kmddk/flowerpot/_odata/v2.0/WorkItemRevisions?
expand: Iteration
select: WorkItemId,WorkItemType,Iteration,CycleTimeDays,ChangedDate
filter: Iteration%2FStartDate%20gt%202019-01-01Z
user: $FEED_PAT
pwd: $FEED_PAT


!## Cycle time
The ability to react to chaning conditions as well as the ability to sustain a high due date performance is strongly linked to cycle time.
`wait in queue` is likewise correlated with cycle time and all else being equal a shorter cycle time will improve performace.
if cycle time increases it's very likely that we can accept substantial decrease in individual performance without impacting the teams performance
## Reading the graph
If we are improving there will be an downwards trend. The two lines represent a shorter and a longer persepctive.
The trend of the "long" is a stronger signal however if the short has recently changed direction, to the opposite of the long trand, this might be a sign that the long trend will change in the near future
!#


only ((WorkItemType = 'User Story') && !(CycleTimeDays = ''))
rename column "Iteration.IterationLevel3" "Sprint Name"
group by "Sprint Name" WorkItemId -> maxby ChangedDate
create column SprintNumber (int (regex ["Sprint Name"] /[Ss][Pp][Rr][Ii][Nn][Tt] [^\\d]*([\\d]+).*/ [$1]))
slice columns "Sprint Name" SprintNumber CycleTimeDays
create column tick 1
rename column CycleTimeDays temp
group by "SprintNumber" -> sum