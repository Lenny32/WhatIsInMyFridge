# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY global.json ./
COPY WhatIsInMyFridge.sln ./
COPY backend/WhatIsInMyFridge.Api/WhatIsInMyFridge.Api.csproj ./backend/WhatIsInMyFridge.Api/
COPY backend/WhatIsInMyFridge.ServiceDefaults/WhatIsInMyFridge.ServiceDefaults.csproj ./backend/WhatIsInMyFridge.ServiceDefaults/

# Restore dependencies
RUN dotnet restore backend/WhatIsInMyFridge.Api/WhatIsInMyFridge.Api.csproj

# Copy everything else
COPY backend/ ./backend/

# Build the application
RUN dotnet publish backend/WhatIsInMyFridge.Api/WhatIsInMyFridge.Api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copy the published app
COPY --from=build /app/publish .

# Create data directory for SQLite database
RUN mkdir -p /app/data

# Expose port (Railway will provide this via PORT env var)
EXPOSE 8080

# Set environment variable for ASP.NET to listen on Railway's PORT
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "WhatIsInMyFridge.Api.dll"]
