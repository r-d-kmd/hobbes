rg=cluster-rg
ipName=hobbesPublicIP
az network public-ip create \
    --resource-group $rg \
    --name $ipName \
    --sku Standard \
    --allocation-method static

publicIP=$(az network public-ip show --resource-group $rg --name $ipName --query ipAddress --output tsv)

clusterID=25d1d14b-c60e-4e06-85e5-300f4c5fd0d5
subID=6df631a2-f66b-4e70-8f3c-4630dca28cbf
az role assignment create \
    --assignee $clusterID \
    --role "Network Contributor" \
    --scope /subscriptions/$subID/resourceGroups/$rg