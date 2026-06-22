# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/OrderSystem.Contracts/OrderSystem.Contracts.csproj", "src/OrderSystem.Contracts/"]

COPY ["src/OrdersService/OrdersService.Api/OrdersService.Api.csproj", "src/OrdersService/OrdersService.Api/"]
COPY ["src/OrdersService/OrdersService.Application/OrdersService.Application.csproj", "src/OrdersService/OrdersService.Application/"]
COPY ["src/OrdersService/OrdersService.Domain/OrdersService.Domain.csproj", "src/OrdersService/OrdersService.Domain/"]
COPY ["src/OrdersService/OrdersService.Infrastructure/OrdersService.Infrastructure.csproj", "src/OrdersService/OrdersService.Infrastructure/"]

RUN dotnet restore "src/OrdersService/OrdersService.Api/OrdersService.Api.csproj"

COPY . .

WORKDIR "/src/src/OrdersService/OrdersService.Api"

RUN dotnet publish "OrdersService.Api.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM runtime AS final
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrdersService.Api.dll"]