# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/OrderSystem.Contracts/OrderSystem.Contracts.csproj", "src/OrderSystem.Contracts/"]

COPY ["src/InventoryService/InventoryService.Api/InventoryService.Api.csproj", "src/InventoryService/InventoryService.Api/"]
COPY ["src/InventoryService/InventoryService.Application/InventoryService.Application.csproj", "src/InventoryService/InventoryService.Application/"]
COPY ["src/InventoryService/InventoryService.Domain/InventoryService.Domain.csproj", "src/InventoryService/InventoryService.Domain/"]
COPY ["src/InventoryService/InventoryService.Infrastructure/InventoryService.Infrastructure.csproj", "src/InventoryService/InventoryService.Infrastructure/"]

RUN dotnet restore "src/InventoryService/InventoryService.Api/InventoryService.Api.csproj"

COPY . .

WORKDIR "/src/src/InventoryService/InventoryService.Api"

RUN dotnet publish "InventoryService.Api.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM runtime AS final
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "InventoryService.Api.dll"]