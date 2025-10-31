# WhatIsInMyFridge - Quick Reference

## 🌐 Application URLs

### Frontend (Svelte SPA)
**https://wonderful-dune-047992a03.3.azurestaticapps.net**

### Backend API
**https://whatsinmyfridge-api.azurewebsites.net**

## 🔧 Azure Resources

| Resource | Name | Type |
|----------|------|------|
| Resource Group | whatsinmyfridge-rg | austriaeast |
| Cosmos DB | whatsinmyfridge-cosmos | Free Tier |
| Storage Account | wimfsto6633 | Standard LRS |
| App Service Plan | whatsinmyfridge-plan | F1 Free |
| Backend API | whatsinmyfridge-api | .NET 9.0 |
| Frontend | whatsinmyfridge-frontend | Static Web App |

## 🚀 Quick Commands

### View All Resources
```powershell
az resource list --resource-group whatsinmyfridge-rg -o table
```

### View App Service Logs
```powershell
az webapp log tail --name whatsinmyfridge-api --resource-group whatsinmyfridge-rg
```

### Restart Backend
```powershell
az webapp restart --name whatsinmyfridge-api --resource-group whatsinmyfridge-rg
```

### Redeploy Frontend
```powershell
cd D:\git\Personal\WhatIsInMyFridge\frontend
deno task build
swa deploy ./dist --env production
```

### Redeploy Backend
```powershell
cd D:\git\Personal\WhatIsInMyFridge\backend\WhatIsInMyFridge.Api
dotnet publish -c Release -o .\publish
Compress-Archive -Path .\publish\* -DestinationPath .\deploy.zip -Force
az webapp deploy --resource-group whatsinmyfridge-rg --name whatsinmyfridge-api --src-path .\deploy.zip --type zip
```

## 💰 Cost

**~$0-2/month** using Azure Free Tier:
- Cosmos DB: $0 (Free Tier)
- App Service: $0 (F1 Free)
- Static Web App: $0 (Free)
- Storage: ~$0.02/GB

## 📱 Access Your App

1. Open: **https://wonderful-dune-047992a03.3.azurestaticapps.net**
2. Register a new account
3. Start managing your fridge inventory!

## 🆘 Troubleshooting

### Frontend not loading?
- Check browser console for errors
- Verify CORS is configured: `az webapp cors show --name whatsinmyfridge-api --resource-group whatsinmyfridge-rg`

### Backend 503 error?
- F1 tier has slow cold starts (5-10 minutes)
- Wait a few minutes and try again
- Check logs: `az webapp log tail --name whatsinmyfridge-api --resource-group whatsinmyfridge-rg`

### Can't connect to database?
- Verify Cosmos DB connection string in App Service configuration
- Check App Service logs for database errors

## 🗑️ Delete Everything

**Warning: This permanently deletes all data!**

```powershell
az group delete --name whatsinmyfridge-rg --yes --no-wait
```

## 📚 More Information

See **AZURE_DEPLOYMENT_SUMMARY.md** for complete details.
