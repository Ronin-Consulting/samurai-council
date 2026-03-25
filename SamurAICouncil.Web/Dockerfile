# SamurAI Council - Multi-project Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Install Kerberos library and curl for health checks
RUN apt-get update && \
    apt-get install -y libgssapi-krb5-2 curl && \
    rm -rf /var/lib/apt/lists/*

USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files first for better layer caching
COPY ["SamurAICouncil.Web/SamurAICouncil.Web.csproj", "SamurAICouncil.Web/"]
COPY ["SamurAICouncil.Core/SamurAICouncil.Core.csproj", "SamurAICouncil.Core/"]
COPY ["SamurAICouncil.Data/SamurAICouncil.Data.csproj", "SamurAICouncil.Data/"]

# Restore dependencies
RUN dotnet restore "SamurAICouncil.Web/SamurAICouncil.Web.csproj"

# Copy remaining source code
COPY . .

# Build and publish
WORKDIR "/src/SamurAICouncil.Web"
RUN dotnet build "SamurAICouncil.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "SamurAICouncil.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "SamurAICouncil.Web.dll"]
