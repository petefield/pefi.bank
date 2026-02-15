FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/Pefi.Bank.Shared/Pefi.Bank.Shared.csproj src/Pefi.Bank.Shared/
COPY src/Pefi.Bank.CustomerPortal/Pefi.Bank.CustomerPortal.csproj src/Pefi.Bank.CustomerPortal/
RUN dotnet restore src/Pefi.Bank.CustomerPortal/Pefi.Bank.CustomerPortal.csproj

COPY src/Pefi.Bank.Shared/ src/Pefi.Bank.Shared/
COPY src/Pefi.Bank.CustomerPortal/ src/Pefi.Bank.CustomerPortal/
RUN dotnet publish src/Pefi.Bank.CustomerPortal/Pefi.Bank.CustomerPortal.csproj -c Release -o /app

FROM nginx:alpine
COPY --from=build /app/wwwroot /usr/share/nginx/html
COPY src/Pefi.Bank.CustomerPortal/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
