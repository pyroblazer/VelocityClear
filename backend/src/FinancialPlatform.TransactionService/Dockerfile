# =============================================================================
# Dockerfile for Transaction Service
# EF Core migrations are applied automatically on startup via MigrateAsync().
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/src/FinancialPlatform.Shared/FinancialPlatform.Shared.csproj                         FinancialPlatform.Shared/
COPY backend/src/FinancialPlatform.EventInfrastructure/FinancialPlatform.EventInfrastructure.csproj FinancialPlatform.EventInfrastructure/
COPY backend/src/FinancialPlatform.TransactionService/FinancialPlatform.TransactionService.csproj FinancialPlatform.TransactionService/

RUN dotnet restore FinancialPlatform.TransactionService/FinancialPlatform.TransactionService.csproj

COPY backend/src/ .

RUN dotnet publish FinancialPlatform.TransactionService/FinancialPlatform.TransactionService.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

USER $APP_UID

EXPOSE 5001

ENTRYPOINT ["dotnet", "FinancialPlatform.TransactionService.dll"]
