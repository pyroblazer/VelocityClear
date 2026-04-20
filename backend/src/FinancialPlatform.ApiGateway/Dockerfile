# =============================================================================
# Dockerfile for API Gateway Service
# Multi-stage build: compile with SDK, run with smaller ASP.NET runtime.
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/src/FinancialPlatform.Shared/FinancialPlatform.Shared.csproj                         FinancialPlatform.Shared/
COPY backend/src/FinancialPlatform.EventInfrastructure/FinancialPlatform.EventInfrastructure.csproj FinancialPlatform.EventInfrastructure/
COPY backend/src/FinancialPlatform.ApiGateway/FinancialPlatform.ApiGateway.csproj                 FinancialPlatform.ApiGateway/

RUN dotnet restore FinancialPlatform.ApiGateway/FinancialPlatform.ApiGateway.csproj

COPY backend/src/ .

RUN dotnet publish FinancialPlatform.ApiGateway/FinancialPlatform.ApiGateway.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

USER $APP_UID

EXPOSE 5000

ENTRYPOINT ["dotnet", "FinancialPlatform.ApiGateway.dll"]
