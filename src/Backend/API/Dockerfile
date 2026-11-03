# Generate publishable configuration without developer credentials. This stage
# stays out of the runtime image and never changes the local settings files.
FROM python:3.12-slim AS settings
COPY src/Backend/API/appsettings*.json /settings/
COPY deploy/api/sanitize_settings.py /sanitize_settings.py
RUN python /sanitize_settings.py /settings /sanitized

# Stage 1: Build & Restore
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo/src/Backend

# Copy csproj files for layer caching
COPY ["src/Backend/Entities/Entities.csproj", "Entities/"]
COPY ["src/Backend/Repositories/Repositories.csproj", "Repositories/"]
COPY ["src/Backend/Services/Services.csproj", "Services/"]
COPY ["src/Backend/API/API.csproj", "API/"]

RUN dotnet restore "API/API.csproj"

# Copy all source files and publish
COPY src/Backend/ .
WORKDIR "/repo/src/Backend/API"
RUN dotnet publish "API.csproj" -c Release -o /app/publish /p:UseAppHost=false
COPY --from=settings /sanitized/ /app/publish/

# Stage 2: Production Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "API.dll"]
