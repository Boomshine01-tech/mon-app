# ============================================
# Stage 1: Build de l'application
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copier les fichiers .csproj et restaurer les dépendances
COPY ["Server/SmartNest.Server.csproj", "Server/"]

# Restaurer les dépendances du projet Server (qui référence Client)
RUN dotnet restore "Server/SmartNest.Server.csproj"

# Copier tout le reste du code source
COPY . .

# Build du projet Server en mode Release
WORKDIR "/src/Server"
RUN dotnet build "SmartNest.Server.csproj" -c Release -o /app/build

# ============================================
# Stage 2: Publication de l'application
# ============================================
FROM build AS publish
WORKDIR "/src/Server"
RUN dotnet publish "SmartNest.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ============================================
# Stage 3: Image finale pour l'exécution
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Exposer le port 80 (HTTP)
EXPOSE 80
EXPOSE 443

# Optimisation pour 512 MB RAM de Render
ENV DOTNET_GCHeapHardLimit=400000000
ENV DOTNET_GCConserveMemory=9
ENV ASPNETCORE_URLS=http://0.0.0.0:$PORT

EXPOSE $PORT

# Copier les fichiers publiés depuis l'étape de publication
COPY --from=publish /app/publish .

# Point d'entrée de l'application
ENTRYPOINT ["dotnet", "SmartNest.Server.dll"]