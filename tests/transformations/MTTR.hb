provider: odata
url: https://analytics.dev.azure.com/kmddk/kmdlogic/_odata/v2.0/WorkItemRevisions?
expand: Iteration
select: WorkItemId,WorkItemType,Iteration,LeadTimeDays,ChangedDate
filter: Iteration/StartDate gt 2019-01-01Z
user: $AZURE_DEVOPS_PAT 
pwd: $AZURE_DEVOPS_PAT


!## Mean time to recover (MTTR)
Research shows that company that are fast at resolving issues after they have been reported also perform well from a business perspective
## Reading the graph
If we are improving there will be an downwards trend. The two lines represent a shorter and a longer persepctive. 
The trend of the "long" is a stronger signal however if the short has recently changed direction, to the opposite of the long trand, this might be a sign that the long trend will change in the near future
!#


only ((WorkItemType = 'Bug') && !(LeadTimeDays = ''))
rename column "Iteration.IterationLevel2" "Sprint Name"
group by "Sprint Name" WorkItemId -> maxby ChangedDate
create column SprintNumber (int (regex ["Sprint Name"] /[Ss][Pp][Rr][Ii][Nn][Tt] [^\\d]*([\\d]+).*/ [$1]))
slice columns "Sprint Name" SprintNumber LeadTimeDays
create column tick 1
group by "SprintNumber" -> sum
rename column LeadTimeDays temp
create column LeadTimeDays (temp / tick)
rename column tick Count
create column SprintNumber keys
sort by column SprintNumber
index rows by SprintNumber
create column "Mean time to recover 5" (moving mean 5 [LeadTimeDays])
create column "Mean time to recover 11" (moving mean 11 [LeadTimeDays])
create column "Mean bugs resolved pr sprint" (moving mean 5 [Count])
slice columns "Mean time to recover 5" "Mean time to recover 11" "Mean bugs resolved pr sprint"
only !("Mean time to recover 11" = missing)


!### Method
THe graph shows the average lead time of bugs. That is the elapsed time from when the bug was reported til it was marked as resolved. 
This is not MTTR because for the bug to be fixed it needs to be deployed to production however since we are trying to do CD bug lead time should be strongly correlated to MTTR
!#