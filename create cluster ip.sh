az network public-ip create \
    --resource-group $rg \
    --name $ipName \
    --sku Standard \
    --allocation-method static

publicIP=$(az network public-ip show --resource-group $rg --name $ipName --query ipAddress --output tsv)


az role assignment create \
    --assignee $clusterID \
    --role "Network Contributor" \
    --scope /subscriptions/$subID/resourceGroups/$rg