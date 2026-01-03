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

RUN mkdir -p /app/data && chmod 777 /app/data

COPY --from=build /app/server .
COPY --from=build /app/client/wwwroot ./wwwroot

ENV PORT=8080
ENV DOTNET_GCHeapHardLimit=400000000
ENV DOTNET_GCConserveMemory=9
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE ${PORT}
ENTRYPOINT ["dotnet", "SmartNest.Server.dll"]