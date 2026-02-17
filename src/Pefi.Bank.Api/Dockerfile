FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/Pefi.Bank.Domain/Pefi.Bank.Domain.csproj src/Pefi.Bank.Domain/
COPY src/Pefi.Bank.Shared/Pefi.Bank.Shared.csproj src/Pefi.Bank.Shared/
COPY src/Pefi.Bank.Infrastructure/Pefi.Bank.Infrastructure.csproj src/Pefi.Bank.Infrastructure/
COPY src/Pefi.Bank.Auth/Pefi.Bank.Auth.csproj src/Pefi.Bank.Auth/
COPY src/Pefi.Bank.Api/Pefi.Bank.Api.csproj src/Pefi.Bank.Api/
RUN dotnet restore src/Pefi.Bank.Api/Pefi.Bank.Api.csproj

COPY src/Pefi.Bank.Domain/ src/Pefi.Bank.Domain/
COPY src/Pefi.Bank.Shared/ src/Pefi.Bank.Shared/
COPY src/Pefi.Bank.Infrastructure/ src/Pefi.Bank.Infrastructure/
COPY src/Pefi.Bank.Auth/ src/Pefi.Bank.Auth/
COPY src/Pefi.Bank.Api/ src/Pefi.Bank.Api/
RUN dotnet publish src/Pefi.Bank.Api/Pefi.Bank.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pefi.Bank.Api.dll"]
