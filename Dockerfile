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

# Create a non-root user for security
RUN adduser --disabled-password --home /app appuser

# Create logs directory and give permissions before switching user
RUN mkdir -p /app/logs && chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Environment variables for production
ENV ASPNETCORE_URLS=http://0.0.0.0:5002 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_ENVIRONMENT=Production \
    Logging__Console__FormatterName=json \
    Logging__LogLevel__Default=Warning \
    Logging__File__Path=/app/logs/app.log \
    Logging__File__RollingInterval=Day \
    Logging__File__FileSizeLimitBytes=10485760 \
    Logging__File__RetainedFileCountLimit=30

# Copy published files
COPY --from=build /app/publish ./

# Expose API port
EXPOSE 5002

# Optional: healthcheck endpoint
HEALTHCHECK --interval=30s --timeout=5s CMD curl -f http://localhost:5002/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "Tracker.Api.dll"]
