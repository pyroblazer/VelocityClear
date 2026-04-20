# =============================================================================
# Dockerfile for Risk Service
# Stateless service - risk state lives in memory (velocity tracker).
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/src/FinancialPlatform.Shared/FinancialPlatform.Shared.csproj                         FinancialPlatform.Shared/
COPY backend/src/FinancialPlatform.EventInfrastructure/FinancialPlatform.EventInfrastructure.csproj FinancialPlatform.EventInfrastructure/
COPY backend/src/FinancialPlatform.RiskService/FinancialPlatform.RiskService.csproj               FinancialPlatform.RiskService/

RUN dotnet restore FinancialPlatform.RiskService/FinancialPlatform.RiskService.csproj

COPY backend/src/ .

RUN dotnet publish FinancialPlatform.RiskService/FinancialPlatform.RiskService.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

USER $APP_UID

EXPOSE 5002

ENTRYPOINT ["dotnet", "FinancialPlatform.RiskService.dll"]
