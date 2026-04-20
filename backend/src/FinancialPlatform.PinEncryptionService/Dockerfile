# =============================================================================
# Dockerfile for PIN Encryption Service
# Stateless service - HSM key store held in memory, seeded on startup.
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/src/FinancialPlatform.Shared/FinancialPlatform.Shared.csproj                                 FinancialPlatform.Shared/
COPY backend/src/FinancialPlatform.EventInfrastructure/FinancialPlatform.EventInfrastructure.csproj       FinancialPlatform.EventInfrastructure/
COPY backend/src/FinancialPlatform.PinEncryptionService/FinancialPlatform.PinEncryptionService.csproj     FinancialPlatform.PinEncryptionService/

RUN dotnet restore FinancialPlatform.PinEncryptionService/FinancialPlatform.PinEncryptionService.csproj

COPY backend/src/ .

RUN dotnet publish FinancialPlatform.PinEncryptionService/FinancialPlatform.PinEncryptionService.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

USER $APP_UID

EXPOSE 5005

ENTRYPOINT ["dotnet", "FinancialPlatform.PinEncryptionService.dll"]
