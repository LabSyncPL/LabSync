# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy csproj and restore
COPY ["src/LabSync.Server/LabSync.Server.csproj", "src/LabSync.Server/"]
COPY ["src/LabSync.Core/LabSync.Core.csproj", "src/LabSync.Core/"]
RUN dotnet restore "src/LabSync.Server/LabSync.Server.csproj"

# Copy everything and build
COPY . .
WORKDIR "/app/src/LabSync.Server"
RUN dotnet publish "LabSync.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install FFmpeg and other dependencies
RUN apt-get update && apt-get install -y ffmpeg libssl-dev libgdiplus && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Environment variables
ENV ASPNETCORE_URLS=http://+:5038
EXPOSE 5038

ENTRYPOINT ["dotnet", "LabSync.Server.dll"]
