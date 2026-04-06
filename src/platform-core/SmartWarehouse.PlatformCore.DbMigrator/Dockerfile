# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY . .
RUN dotnet publish src/platform-core/SmartWarehouse.PlatformCore.DbMigrator/SmartWarehouse.PlatformCore.DbMigrator.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime
WORKDIR /app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SmartWarehouse.PlatformCore.DbMigrator.dll"]
