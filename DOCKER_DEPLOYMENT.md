# Docker Deployment Guide

## Quick Start

### 1. Pull the latest image
```bash
docker pull ghcr.io/yourusername/whatsinmyfridge:latest
```

### 2. Set up environment variables
```bash
cp .env.example .env
# Edit .env with your configuration
```

Required variables:
- `JWT_SECRET_KEY` - Secret key for JWT tokens (min 32 characters)
- `COSMOS_CONNECTION_STRING` - Azure Cosmos DB connection string

Optional variables:
- `BLOB_STORAGE_CONNECTION_STRING` - Azure Blob Storage (omit to use local storage)
- `CORS_ORIGINS` - Allowed origins (default: `*`)
- `PORT` - Port to expose (default: `80`)

### 3. Run with docker-compose
```bash
docker-compose -f docker-compose.simple.yml up -d
```

Or run directly:
```bash
docker run -d \
  --name whatsinmyfridge \
  -p 80:80 \
  -e JWT_SECRET_KEY="your-secret-key-min-32-chars" \
  -e COSMOS_CONNECTION_STRING="your-cosmos-connection" \
  -v whatsinmyfridge-data:/app/data \
  --restart unless-stopped \
  ghcr.io/yourusername/whatsinmyfridge:latest
```

### 4. Access the app
Open http://localhost in your browser.

---

## Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `JWT_SECRET_KEY` | ✅ Yes | - | JWT signing key (minimum 32 characters) |
| `COSMOS_CONNECTION_STRING` | ✅ Yes | - | Azure Cosmos DB connection string |
| `BLOB_STORAGE_CONNECTION_STRING` | ❌ No | - | Azure Blob Storage connection (for photos) |
| `CORS_ORIGINS` | ❌ No | `*` | Comma-separated list of allowed origins |
| `ASPNETCORE_ENVIRONMENT` | ❌ No | `Production` | ASP.NET Core environment |

### Generate JWT Secret
```bash
openssl rand -base64 32
```

### Get Azure Cosmos DB Connection String
```bash
az cosmosdb keys list \
  --name your-cosmos-account \
  --resource-group your-rg \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" -o tsv
```

---

## Deployment Examples

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
    COSMOS_CONNECTION_STRING="your-connection" \
  --secure-environment-variables \
    JWT_SECRET_KEY="your-secret"
```

### AWS ECS/Fargate
Create a task definition with the image and environment variables, then create a service.

### Railway
```bash
# Install Railway CLI
npm i -g @railway/cli

# Login and create project
railway login
railway init

# Deploy from Docker image
railway up --image ghcr.io/yourusername/whatsinmyfridge:latest

# Set environment variables
railway variables set JWT_SECRET_KEY="your-secret"
railway variables set COSMOS_CONNECTION_STRING="your-connection"
```

### Fly.io
Create `fly.toml`:
```toml
app = "whatsinmyfridge"

[build]
  image = "ghcr.io/yourusername/whatsinmyfridge:latest"

[[services]]
  internal_port = 80
  protocol = "tcp"

  [[services.ports]]
    handlers = ["http"]
    port = 80

  [[services.ports]]
    handlers = ["tls", "http"]
    port = 443

[env]
  ASPNETCORE_ENVIRONMENT = "Production"
```

Deploy:
```bash
flyctl launch
flyctl secrets set JWT_SECRET_KEY="your-secret"
flyctl secrets set COSMOS_CONNECTION_STRING="your-connection"
flyctl deploy
```

### DigitalOcean App Platform
1. Create new app from Docker Hub
2. Set image: `ghcr.io/yourusername/whatsinmyfridge:latest`
3. Set environment variables
4. Deploy

### Google Cloud Run
```bash
gcloud run deploy whatsinmyfridge \
  --image ghcr.io/yourusername/whatsinmyfridge:latest \
  --platform managed \
  --region us-central1 \
  --allow-unauthenticated \
  --set-env-vars JWT_SECRET_KEY="your-secret" \
  --set-env-vars COSMOS_CONNECTION_STRING="your-connection"
```

---

## Health Checks

The container exposes a health endpoint at `/health`.

Docker Compose health check:
```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost/health"]
  interval: 30s
  timeout: 10s
  retries: 3
  start_period: 40s
```

---

## Volumes

### Data Directory
The container uses `/app/data` for local file storage (when Blob Storage is not configured).

Persist this directory:
```bash
docker run -v whatsinmyfridge-data:/app/data ...
```

Or mount a host directory:
```bash
docker run -v /path/on/host:/app/data ...
```

---

## CORS Configuration

By default, CORS is set to `*` (allow all origins). For production, specify allowed origins:

```bash
# Single origin
-e CORS_ORIGINS="https://fridge.colen.at"

# Multiple origins
-e CORS_ORIGINS="https://fridge.colen.at,https://colen.at"
```

---

## Updating

### Pull latest image
```bash
docker-compose -f docker-compose.simple.yml pull
docker-compose -f docker-compose.simple.yml up -d
```

### Specific version
```bash
docker pull ghcr.io/yourusername/whatsinmyfridge:v1.2.3
docker-compose -f docker-compose.simple.yml up -d
```

---

## Troubleshooting

### View logs
```bash
docker logs whatsinmyfridge -f
```

### Check health
```bash
curl http://localhost/health
```

### Access container shell
```bash
docker exec -it whatsinmyfridge bash
```

### Check environment variables
```bash
docker exec whatsinmyfridge env
```

### Common issues

**Container exits immediately:**
- Check JWT_SECRET_KEY is at least 32 characters
- Verify Cosmos DB connection string is valid

**Cannot connect to API:**
- Check port mapping: `-p 80:80`
- Verify firewall rules
- Check container logs

**CORS errors:**
- Set CORS_ORIGINS to your frontend domain
- Don't use `*` in production

---

## Tags

Available image tags:
- `latest` - Latest main branch build
- `main` - Main branch
- `v1.2.3` - Semantic version tags
- `main-abc123` - Specific commit SHA

---

## Building Locally

```bash
# Build unified image
docker build -f Dockerfile.unified -t whatsinmyfridge .

# Run locally built image
docker run -d \
  --name whatsinmyfridge \
  -p 80:80 \
  -e JWT_SECRET_KEY="your-secret" \
  -e COSMOS_CONNECTION_STRING="your-connection" \
  whatsinmyfridge
```

---

## Security Best Practices

1. **Never commit secrets** - Use environment variables or secrets management
2. **Use HTTPS** - Deploy behind a reverse proxy with SSL
3. **Restrict CORS** - Set specific origins, not `*`
4. **Use strong JWT secret** - Minimum 32 characters, random
5. **Regular updates** - Pull latest images regularly
6. **Scan images** - Use `docker scan` or security tools
7. **Network isolation** - Use Docker networks to isolate containers
8. **Read-only filesystem** - Add `--read-only` flag where possible

---

## Multi-architecture Support

The image is built for both `linux/amd64` and `linux/arm64`, supporting:
- x86_64 servers (Intel/AMD)
- ARM servers (AWS Graviton, Apple Silicon, Raspberry Pi 4+)

Docker automatically pulls the correct architecture.

---

For more deployment options, see [DEPLOYMENT.md](DEPLOYMENT.md).
