# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["LabSync.Server/LabSync.Server.csproj", "LabSync.Server/"]
COPY ["LabSync.Core/LabSync.Core.csproj", "LabSync.Core/"]

RUN dotnet restore "LabSync.Server/LabSync.Server.csproj"

COPY . .
WORKDIR /src/LabSync.Server
RUN dotnet publish "LabSync.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet", "LabSync.Server.dll"]
