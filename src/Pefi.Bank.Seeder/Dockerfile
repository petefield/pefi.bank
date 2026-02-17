FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/Pefi.Bank.Shared/Pefi.Bank.Shared.csproj src/Pefi.Bank.Shared/
COPY src/Pefi.Bank.Seeder/Pefi.Bank.Seeder.csproj src/Pefi.Bank.Seeder/
RUN dotnet restore src/Pefi.Bank.Seeder/Pefi.Bank.Seeder.csproj

COPY src/Pefi.Bank.Shared/ src/Pefi.Bank.Shared/
COPY src/Pefi.Bank.Seeder/ src/Pefi.Bank.Seeder/
RUN dotnet publish src/Pefi.Bank.Seeder/Pefi.Bank.Seeder.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Pefi.Bank.Seeder.dll"]
