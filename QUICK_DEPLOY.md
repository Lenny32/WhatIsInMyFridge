# 🐳 Quick Deployment - Docker Container

**The simplest way to deploy WhatIsInMyFridge!**

Everything (frontend + backend) runs in a single Docker container. Just set environment variables and go!

## ⚡ Quick Start

### 1. Create environment file
```bash
# Copy example file
cp .env.example .env

# Edit with your values
nano .env
```

### 2. Run with Docker Compose
```bash
docker-compose -f docker-compose.simple.yml up -d
```

### 3. Access the app
Open http://localhost in your browser! 🎉

---

## 🔑 Required Setup

### Generate JWT Secret
```bash
openssl rand -base64 32
```

### Create Azure Cosmos DB (Free Tier)
```bash
# Login to Azure
az login

# Create resource group
az group create --name WhatIsInMyFridge-RG --location eastus

# Create free tier Cosmos DB
az cosmosdb create \
  --name whatsinmyfridge \
  --resource-group WhatIsInMyFridge-RG \
  --enable-free-tier true

# Get connection string
az cosmosdb keys list \
  --name whatsinmyfridge \
  --resource-group WhatIsInMyFridge-RG \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" -o tsv
```

---

## 📦 What's in the Container?

- ✅ Backend .NET API (port 5000 internally)
- ✅ Frontend Svelte app
- ✅ Nginx reverse proxy (serves frontend, proxies /api to backend)
- ✅ Health checks
- ✅ Multi-architecture support (amd64/arm64)

---

## 🚀 Deployment Options

### Local/Development
```bash
docker-compose -f docker-compose.simple.yml up -d
```

### Azure Container Instances
```bash
az container create \
  --resource-group WhatIsInMyFridge-RG \
  --name whatsinmyfridge \
  --image ghcr.io/yourusername/whatsinmyfridge:latest \
  --dns-name-label whatsinmyfridge \
  --ports 80 \
  --environment-variables \
    JWT_SECRET_KEY="your-secret" \
    COSMOS_CONNECTION_STRING="your-connection"
```

### Railway
```bash
railway up --image ghcr.io/yourusername/whatsinmyfridge:latest
railway variables set JWT_SECRET_KEY="your-secret"
railway variables set COSMOS_CONNECTION_STRING="your-connection"
```

### Fly.io
```bash
flyctl launch --image ghcr.io/yourusername/whatsinmyfridge:latest
flyctl secrets set JWT_SECRET_KEY="your-secret"
flyctl secrets set COSMOS_CONNECTION_STRING="your-connection"
```

### Google Cloud Run
```bash
gcloud run deploy whatsinmyfridge \
  --image ghcr.io/yourusername/whatsinmyfridge:latest \
  --platform managed \
  --allow-unauthenticated \
  --set-env-vars JWT_SECRET_KEY="your-secret",COSMOS_CONNECTION_STRING="your-connection"
```

---

## 🔧 Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `JWT_SECRET_KEY` | ✅ | JWT secret (min 32 chars) |
| `COSMOS_CONNECTION_STRING` | ✅ | Cosmos DB connection |
| `BLOB_STORAGE_CONNECTION_STRING` | ❌ | Blob Storage (optional) |
| `CORS_ORIGINS` | ❌ | Allowed origins (default: `*`) |

---

## 🏗️ How It Works

1. **GitHub Actions** builds the Docker image on every push to `main`
2. Image is pushed to **GitHub Container Registry** (ghcr.io)
3. Pull the image and run anywhere Docker runs!

### Build Workflow
- Triggered on push to `main` or version tags (`v*`)
- Builds unified container with frontend + backend
- Publishes to `ghcr.io/yourusername/whatsinmyfridge:latest`
- Automatically tests the image
- Creates multi-arch builds (amd64 + arm64)

---

## 📋 Common Commands

### View logs
```bash
docker logs whatsinmyfridge -f
```

### Check health
```bash
curl http://localhost/health
```

### Update to latest
```bash
docker-compose -f docker-compose.simple.yml pull
docker-compose -f docker-compose.simple.yml up -d
```

### Stop
```bash
docker-compose -f docker-compose.simple.yml down
```

---

## 📚 Full Documentation

- [DOCKER_DEPLOYMENT.md](DOCKER_DEPLOYMENT.md) - Detailed Docker deployment guide
- [DEPLOYMENT.md](DEPLOYMENT.md) - Alternative deployment methods
- [.env.example](.env.example) - Environment variable template

---

## 🎯 Benefits of Docker Deployment

✅ **Single Container** - Everything in one place  
✅ **Easy Configuration** - Just environment variables  
✅ **Portable** - Run anywhere Docker runs  
✅ **Consistent** - Same image everywhere  
✅ **Automated Builds** - GitHub Actions builds on every push  
✅ **Multi-arch** - Works on x86 and ARM  
✅ **Health Checks** - Built-in monitoring  
✅ **No Build Steps** - Just pull and run!

---

## 🆘 Troubleshooting

**Container exits immediately?**
- Check JWT_SECRET_KEY is at least 32 characters
- Verify Cosmos DB connection string

**Can't connect to API?**
- Check port mapping: `-p 80:80`
- Verify `docker ps` shows container is running
- Check logs: `docker logs whatsinmyfridge`

**CORS errors?**
- Set `CORS_ORIGINS` to your domain (don't use `*` in production)

---

Made with ❤️ | Questions? Open an issue!
