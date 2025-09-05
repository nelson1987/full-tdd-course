FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Instalar ferramentas para health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Pigbank.Api.csproj", "."]
RUN dotnet restore "./Pigbank.Api.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./Pigbank.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Pigbank.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Configurar user não-root para segurança
RUN adduser --disabled-password --home /app --gecos '' --shell /bin/bash dotnetuser && chown -R dotnetuser /app
USER dotnetuser

ENTRYPOINT ["dotnet", "Pigbank.Api.dll"]