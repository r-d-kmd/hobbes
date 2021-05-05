provider: odata
url: https://analytics.dev.azure.com/kmddk/flowerpot/_odata/v2.0/WorkItemRevisions?
expand: Iteration
select: State,ChangedDate,WorkITemId,WorkItemType,State,StateCategory,Iteration,Title,CreatedDate,ClosedDate,LeadTimeDays,CycleTimeDays,RevisedDate
filter: Iteration%2FStartDate%20gt%202019-01-01Z
user: $AZURE_DEVOPS_PAT
pwd: $AZURE_DEVOPS_PAT
meta:
  category: workitems
  name: flowerpot


only (WorkItemType = 'User Story')
group by "Iteration.IterationName" WorkItemId -> maxby ChangedDate
rename column "Iteration.IterationLevel2" "Sprint Name"
create column SprintNumber (int (regex ["Sprint Name"] /[Ii][Tt][Ee][Rr][Aa][Tt][Ii][Oo][Nn] [^\\d]*([\\d]+).*/ [$1]))
rename column State "DetailedState"
create column State (if [StateCategory = 'Completed' || StateCategory = 'Resolved' || StateCategory = 'Remove'] {'Done'} else { if ["StateCategory" = 'InProgress'] {'Doing'} else {'Todo'} })
slice columns "Sprint Name" "WorkItemId" "ChangedDate" "WorkItemType" "CreatedDate" "ClosedDate" "LeadTimeDays" "CycleTimeDays" "StoryPoints" "RevisedDate" "Priority" "Title" "Sprint Number" "State"