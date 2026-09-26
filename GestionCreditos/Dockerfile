# Etapa 1: compilar la aplicación
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "GestionCreditos.csproj"
RUN dotnet publish "GestionCreditos.csproj" -c Release -o /app/publish

# Etapa 2: imagen final, más liviana, solo para ejecutar
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "GestionCreditos.dll"]