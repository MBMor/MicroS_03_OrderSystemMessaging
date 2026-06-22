# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/OrderSystem.Contracts/OrderSystem.Contracts.csproj", "src/OrderSystem.Contracts/"]

COPY ["src/NotificationsService/NotificationsService.Api/NotificationsService.Api.csproj", "src/NotificationsService/NotificationsService.Api/"]
COPY ["src/NotificationsService/NotificationsService.Application/NotificationsService.Application.csproj", "src/NotificationsService/NotificationsService.Application/"]
COPY ["src/NotificationsService/NotificationsService.Domain/NotificationsService.Domain.csproj", "src/NotificationsService/NotificationsService.Domain/"]
COPY ["src/NotificationsService/NotificationsService.Infrastructure/NotificationsService.Infrastructure.csproj", "src/NotificationsService/NotificationsService.Infrastructure/"]

RUN dotnet restore "src/NotificationsService/NotificationsService.Api/NotificationsService.Api.csproj"

COPY . .

WORKDIR "/src/src/NotificationsService/NotificationsService.Api"

RUN dotnet publish "NotificationsService.Api.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM runtime AS final
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NotificationsService.Api.dll"]