#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["HLS.Paygate.Backend.Hosting/HLS.Paygate.Backend.Hosting.csproj", "HLS.Paygate.Backend.Hosting/"]
COPY ["HLS.Paygate.Backend.Interface/HLS.Paygate.Backend.Interface.csproj", "HLS.Paygate.Backend.Interface/"]
COPY ["HLS.Paygate.Gw.Domain/HLS.Paygate.Gw.Domain.csproj", "HLS.Paygate.Gw.Domain/"]
COPY ["HLS.Paygate.Gw.Model/HLS.Paygate.Gw.Model.csproj", "HLS.Paygate.Gw.Model/"]
COPY ["HLS.Paygate.Shared/HLS.Paygate.Shared.csproj", "HLS.Paygate.Shared/"]
COPY ["HLS.Paygate.TopupGw.Domains/HLS.Paygate.TopupGw.Domains.csproj", "HLS.Paygate.TopupGw.Domains/"]
COPY ["HLS.Paygate.TopupGw.Contacts/HLS.Paygate.TopupGw.Contacts.csproj", "HLS.Paygate.TopupGw.Contacts/"]
COPY ["Paygate.Contracts/Paygate.Contracts.csproj", "Paygate.Contracts/"]
COPY ["Paygate.Discovery.Requests/Paygate.Discovery.Requests.csproj", "Paygate.Discovery.Requests/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "HLS.Paygate.Backend.Hosting/HLS.Paygate.Backend.Hosting.csproj"
COPY . .
WORKDIR "/src/HLS.Paygate.Backend.Hosting"
RUN dotnet build "HLS.Paygate.Backend.Hosting.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HLS.Paygate.Backend.Hosting.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HLS.Paygate.Backend.Hosting.dll"]