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
COPY ["Topup.Common.Hosting/Topup.Common.Hosting.csproj", "Topup.Common.Hosting/"]
COPY ["Topup.Common.Interface/Topup.Common.Interface.csproj", "Topup.Common.Interface/"]
COPY ["Topup.Common.Domain/Topup.Common.Domain.csproj", "Topup.Common.Domain/"]
COPY ["Topup.Common.Model/Topup.Common.Model.csproj", "Topup.Common.Model/"]
COPY ["Topup.Shared/Topup.Shared.csproj", "Topup.Shared/"]
COPY ["Topup.Contracts/Topup.Contracts.csproj", "Topup.Contracts/"]
COPY ["Topup.Discovery.Requests/Topup.Discovery.Requests.csproj", "Topup.Discovery.Requests/"]
COPY ["Topup.Gw.Model/Topup.Gw.Model.csproj", "Topup.Gw.Model/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "./Topup.Common.Hosting/Topup.Common.Hosting.csproj"
COPY . .
WORKDIR "/src/Topup.Common.Hosting"
RUN dotnet build "./Topup.Common.Hosting.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Topup.Common.Hosting.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Topup.Common.Hosting.dll"]