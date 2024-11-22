# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Topup.Gw.Hosting/Topup.Gw.Hosting.csproj", "Topup.Gw.Hosting/"]
COPY ["Topup.Gw.Domain/Topup.Gw.Domain.csproj", "Topup.Gw.Domain/"]
COPY ["Topup.Gw.Model/Topup.Gw.Model.csproj", "Topup.Gw.Model/"]
COPY ["Topup.Shared/Topup.Shared.csproj", "Topup.Shared/"]
COPY ["Topup.Discovery.Requests/Topup.Discovery.Requests.csproj", "Topup.Discovery.Requests/"]
COPY ["Topup.Contracts/Topup.Contracts.csproj", "Topup.Contracts/"]
COPY ["Topup.Gw.Interface/Topup.Gw.Interface.csproj", "Topup.Gw.Interface/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "./Topup.Gw.Hosting/Topup.Gw.Hosting.csproj"
COPY . .
WORKDIR "/src/Topup.Gw.Hosting"
RUN dotnet build "./Topup.Gw.Hosting.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Topup.Gw.Hosting.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Topup.Gw.Hosting.dll"]