#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["HLS.Paygate.Common.Hosting/HLS.Paygate.Common.Hosting.csproj", "HLS.Paygate.Common.Hosting/"]
COPY ["HLS.Paygate.Common.Interface/HLS.Paygate.Common.Interface.csproj", "HLS.Paygate.Common.Interface/"]
COPY ["HLS.Paygate.Common.Domain/HLS.Paygate.Common.Domain.csproj", "HLS.Paygate.Common.Domain/"]
COPY ["HLS.Paygate.Common.Model/HLS.Paygate.Common.Model.csproj", "HLS.Paygate.Common.Model/"]
COPY ["HLS.Paygate.Shared/HLS.Paygate.Shared.csproj", "HLS.Paygate.Shared/"]
COPY ["Paygate.Contracts/Paygate.Contracts.csproj", "Paygate.Contracts/"]
COPY ["Paygate.Discovery.Requests/Paygate.Discovery.Requests.csproj", "Paygate.Discovery.Requests/"]
COPY ["HLS.Paygate.Gw.Model/HLS.Paygate.Gw.Model.csproj", "HLS.Paygate.Gw.Model/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "HLS.Paygate.Common.Hosting/HLS.Paygate.Common.Hosting.csproj"
COPY . .
WORKDIR "/src/HLS.Paygate.Common.Hosting"
RUN dotnet build "HLS.Paygate.Common.Hosting.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HLS.Paygate.Common.Hosting.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HLS.Paygate.Common.Hosting.dll"]