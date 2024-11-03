#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["HLS.Paygate.Balance.Hosting/HLS.Paygate.Balance.Hosting.csproj", "HLS.Paygate.Balance.Hosting/"]
COPY ["HLS.Paygate.Balance.Components/HLS.Paygate.Balance.Components.csproj", "HLS.Paygate.Balance.Components/"]
COPY ["HLS.Paygate.Balance.Domain/HLS.Paygate.Balance.Domain.csproj", "HLS.Paygate.Balance.Domain/"]
COPY ["HLS.Paygate.Balance.Models/HLS.Paygate.Balance.Models.csproj", "HLS.Paygate.Balance.Models/"]
COPY ["HLS.Paygate.Shared/HLS.Paygate.Shared.csproj", "HLS.Paygate.Shared/"]
COPY ["Orleans.Sagas/Orleans.Sagas.csproj", "Orleans.Sagas/"]
COPY ["Paygate.Discovery.Requests/Paygate.Discovery.Requests.csproj", "Paygate.Discovery.Requests/"]
COPY ["Paygate.Contracts/Paygate.Contracts.csproj", "Paygate.Contracts/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "./HLS.Paygate.Balance.Hosting/HLS.Paygate.Balance.Hosting.csproj"
COPY . .
WORKDIR "/src/HLS.Paygate.Balance.Hosting"
RUN dotnet build "./HLS.Paygate.Balance.Hosting.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./HLS.Paygate.Balance.Hosting.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HLS.Paygate.Balance.Hosting.dll"]