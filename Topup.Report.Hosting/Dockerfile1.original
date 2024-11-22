#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["HLS.Paygate.Report.Hosting/HLS.Paygate.Report.Hosting.csproj", "HLS.Paygate.Report.Hosting/"]
COPY ["HLS.Paygate.Report.Domain/HLS.Paygate.Report.Domain.csproj", "HLS.Paygate.Report.Domain/"]
COPY ["HLS.Paygate.Report.Model/HLS.Paygate.Report.Model.csproj", "HLS.Paygate.Report.Model/"]
COPY ["HLS.Paygate.Gw.Model/HLS.Paygate.Gw.Model.csproj", "HLS.Paygate.Gw.Model/"]
COPY ["HLS.Paygate.Shared/HLS.Paygate.Shared.csproj", "HLS.Paygate.Shared/"]
COPY ["Paygate.Contracts/Paygate.Contracts.csproj", "Paygate.Contracts/"]
COPY ["Paygate.Discovery.Requests/Paygate.Discovery.Requests.csproj", "Paygate.Discovery.Requests/"]
COPY ["HLS.Paygate.Report.Interface/HLS.Paygate.Report.Interface.csproj", "HLS.Paygate.Report.Interface/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "HLS.Paygate.Report.Hosting/HLS.Paygate.Report.Hosting.csproj"
COPY . .
WORKDIR "/src/HLS.Paygate.Report.Hosting"
RUN dotnet build "HLS.Paygate.Report.Hosting.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HLS.Paygate.Report.Hosting.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HLS.Paygate.Report.Hosting.dll"]