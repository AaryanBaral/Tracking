# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first for better build cache behavior
COPY EmployeeTracker.sln ./
COPY Tracker.Api/Tracker.Api.csproj Tracker.Api/
RUN dotnet restore Tracker.Api/Tracker.Api.csproj

# Copy source and publish API
COPY . .
RUN dotnet publish Tracker.Api/Tracker.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:5000 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_ENVIRONMENT=Production \
    Logging__Console__FormatterName=json \
    Logging__LogLevel__Default=Warning

COPY --from=build /app/publish ./

EXPOSE 5000
ENTRYPOINT ["dotnet", "Tracker.Api.dll"]
