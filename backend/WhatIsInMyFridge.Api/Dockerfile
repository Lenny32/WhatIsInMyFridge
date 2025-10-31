# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["WhatIsInMyFridge.Api/WhatIsInMyFridge.Api.csproj", "WhatIsInMyFridge.Api/"]

# Restore dependencies
RUN dotnet restore "WhatIsInMyFridge.Api/WhatIsInMyFridge.Api.csproj"

# Copy all source files
COPY . .

# Build the application
WORKDIR "/src/WhatIsInMyFridge.Api"
RUN dotnet build "WhatIsInMyFridge.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "WhatIsInMyFridge.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .

# Create data directory for items.json
RUN mkdir -p /app/data

ENTRYPOINT ["dotnet", "WhatIsInMyFridge.Api.dll"]
