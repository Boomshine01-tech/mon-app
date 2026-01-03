FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copier toute la solution
COPY . .

# Build Client
WORKDIR /src/Client
RUN dotnet publish "SmartNest.Client.csproj" -c Release -o /app/client

# Build Server
WORKDIR /src/Server
RUN dotnet publish "SmartNest.Server.csproj" -c Release -o /app/server

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN mkdir -p /app/data 

COPY --from=build /app/server .
COPY --from=build /app/client/wwwroot ./wwwroot


ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "SmartNest.Server.dll"]
