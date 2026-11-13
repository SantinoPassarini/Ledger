# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/Ledger.Api/Ledger.Api.csproj src/Ledger.Api/
RUN dotnet restore src/Ledger.Api/Ledger.Api.csproj

COPY src/Ledger.Api/ src/Ledger.Api/
RUN dotnet publish src/Ledger.Api/Ledger.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=true \
    DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish ./

# The .NET 8 runtime image includes an unprivileged "app" user.
USER app

EXPOSE 10000

# Render terminates TLS and forwards plain HTTP to the port in PORT (10000 by default).
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000} exec dotnet Ledger.Api.dll"]
