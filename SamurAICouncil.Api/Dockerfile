# syntax=docker/dockerfile:1
# Build context = repository root.

# ---- Stage 1: build the Angular SPA ----
FROM node:22-alpine AS ngbuild
WORKDIR /ng
COPY clients/angular/package.json clients/angular/package-lock.json ./
RUN npm ci
COPY clients/angular/ ./
RUN npm run build -- --configuration production

# ---- Stage 2: build/publish the .NET API ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SamurAICouncil.Api/SamurAICouncil.Api.csproj SamurAICouncil.Api/
COPY SamurAICouncil.Core/SamurAICouncil.Core.csproj SamurAICouncil.Core/
COPY SamurAICouncil.Data/SamurAICouncil.Data.csproj SamurAICouncil.Data/
RUN dotnet restore SamurAICouncil.Api/SamurAICouncil.Api.csproj
COPY SamurAICouncil.Api/ SamurAICouncil.Api/
COPY SamurAICouncil.Core/ SamurAICouncil.Core/
COPY SamurAICouncil.Data/ SamurAICouncil.Data/
RUN dotnet publish SamurAICouncil.Api/SamurAICouncil.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# Kerberos lib for SQL Server (Text-to-SQL) + curl for the healthcheck
RUN apt-get update && apt-get install -y libgssapi-krb5-2 curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish ./
# Angular production build → served as static wwwroot with SPA fallback
COPY --from=ngbuild /ng/dist/samurai-council-web/browser ./wwwroot
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SamurAICouncil.Api.dll"]
