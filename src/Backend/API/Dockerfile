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

# Stage 2: Production Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "API.dll"]
