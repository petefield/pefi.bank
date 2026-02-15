FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/Pefi.Bank.Domain/Pefi.Bank.Domain.csproj src/Pefi.Bank.Domain/
COPY src/Pefi.Bank.Shared/Pefi.Bank.Shared.csproj src/Pefi.Bank.Shared/
COPY src/Pefi.Bank.Infrastructure/Pefi.Bank.Infrastructure.csproj src/Pefi.Bank.Infrastructure/
COPY src/Pefi.Bank.Functions/Pefi.Bank.Functions.csproj src/Pefi.Bank.Functions/
RUN dotnet restore src/Pefi.Bank.Functions/Pefi.Bank.Functions.csproj

COPY src/Pefi.Bank.Domain/ src/Pefi.Bank.Domain/
COPY src/Pefi.Bank.Shared/ src/Pefi.Bank.Shared/
COPY src/Pefi.Bank.Infrastructure/ src/Pefi.Bank.Infrastructure/
COPY src/Pefi.Bank.Functions/ src/Pefi.Bank.Functions/
RUN dotnet publish src/Pefi.Bank.Functions/Pefi.Bank.Functions.csproj -c Release -o /app

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0

RUN apt-get update && apt-get install -y --no-install-recommends openssl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

ENV AzureWebJobsScriptRoot=/home/site/wwwroot
COPY --from=build /app /home/site/wwwroot
COPY src/Pefi.Bank.Functions/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
