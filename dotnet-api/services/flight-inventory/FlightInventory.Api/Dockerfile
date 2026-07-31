# Build context is the dotnet-api/ repo root: docker build -f services/flight-inventory/FlightInventory.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet restore "services/flight-inventory/FlightInventory.Api/FlightInventory.Api.csproj"
RUN dotnet build "services/flight-inventory/FlightInventory.Api/FlightInventory.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "services/flight-inventory/FlightInventory.Api/FlightInventory.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FlightInventory.Api.dll"]
