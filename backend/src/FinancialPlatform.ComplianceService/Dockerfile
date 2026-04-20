# =============================================================================
# Dockerfile for Compliance Service
# Persists events to SQL Server with SHA-256 hash chaining.
# Exposes Prometheus metrics at /metrics.
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/src/FinancialPlatform.Shared/FinancialPlatform.Shared.csproj                         FinancialPlatform.Shared/
COPY backend/src/FinancialPlatform.EventInfrastructure/FinancialPlatform.EventInfrastructure.csproj FinancialPlatform.EventInfrastructure/
COPY backend/src/FinancialPlatform.ComplianceService/FinancialPlatform.ComplianceService.csproj   FinancialPlatform.ComplianceService/

RUN dotnet restore FinancialPlatform.ComplianceService/FinancialPlatform.ComplianceService.csproj

COPY backend/src/ .

RUN dotnet publish FinancialPlatform.ComplianceService/FinancialPlatform.ComplianceService.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

USER $APP_UID

EXPOSE 5004

ENTRYPOINT ["dotnet", "FinancialPlatform.ComplianceService.dll"]
