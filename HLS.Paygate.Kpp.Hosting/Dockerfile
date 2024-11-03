#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["HLS.Paygate.Kpp.Hosting/HLS.Paygate.Kpp.Hosting.csproj", "HLS.Paygate.Kpp.Hosting/"]
COPY ["HLS.Paygate.Kpp.Domain/HLS.Paygate.Kpp.Domain.csproj", "HLS.Paygate.Kpp.Domain/"]
COPY ["HLS.Paygate.Shared/HLS.Paygate.Shared.csproj", "HLS.Paygate.Shared/"]
COPY ["Paygate.Contracts/Paygate.Contracts.csproj", "Paygate.Contracts/"]
COPY ["Paygate.Discovery.Requests/Paygate.Discovery.Requests.csproj", "Paygate.Discovery.Requests/"]
COPY ["HLS.Paygate.Gw.Model/HLS.Paygate.Gw.Model.csproj", "HLS.Paygate.Gw.Model/"]
COPY ["HLS.Paygate.Kpp.Interface/HLS.Paygate.Kpp.Interface.csproj", "HLS.Paygate.Kpp.Interface/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "HLS.Paygate.Kpp.Hosting/HLS.Paygate.Kpp.Hosting.csproj"
COPY . .
WORKDIR "/src/HLS.Paygate.Kpp.Hosting"
RUN dotnet build "HLS.Paygate.Kpp.Hosting.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HLS.Paygate.Kpp.Hosting.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HLS.Paygate.Kpp.Hosting.dll"]