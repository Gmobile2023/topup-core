#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["HLS.Paygate.Stock.Hosting/HLS.Paygate.Stock.Hosting.csproj", "HLS.Paygate.Stock.Hosting/"]
COPY ["HLS.Paygate.Stock.Components/HLS.Paygate.Stock.Components.csproj", "HLS.Paygate.Stock.Components/"]
COPY ["HLS.Paygate.Stock.Contracts/HLS.Paygate.Stock.Contracts.csproj", "HLS.Paygate.Stock.Contracts/"]
COPY ["HLS.Paygate.Shared/HLS.Paygate.Shared.csproj", "HLS.Paygate.Shared/"]
COPY ["HLS.Paygate.Stock.Domains/HLS.Paygate.Stock.Domains.csproj", "HLS.Paygate.Stock.Domains/"]
COPY ["Paygate.Discovery.Requests/Paygate.Discovery.Requests.csproj", "Paygate.Discovery.Requests/"]
COPY ["HLS.Paygate.Gw.Model/HLS.Paygate.Gw.Model.csproj", "HLS.Paygate.Gw.Model/"]
COPY ["Paygate.Contracts/Paygate.Contracts.csproj", "Paygate.Contracts/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "HLS.Paygate.Stock.Hosting/HLS.Paygate.Stock.Hosting.csproj"
COPY . .
WORKDIR "/src/HLS.Paygate.Stock.Hosting"
RUN dotnet build "HLS.Paygate.Stock.Hosting.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HLS.Paygate.Stock.Hosting.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HLS.Paygate.Stock.Hosting.dll"]