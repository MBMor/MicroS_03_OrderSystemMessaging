FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/OrderSystem.Contracts/OrderSystem.Contracts.csproj", "src/OrderSystem.Contracts/"]
COPY ["src/NotificationsService/NotificationsService.Domain/NotificationsService.Domain.csproj", "src/NotificationsService/NotificationsService.Domain/"]
COPY ["src/NotificationsService/NotificationsService.Application/NotificationsService.Application.csproj", "src/NotificationsService/NotificationsService.Application/"]
COPY ["src/NotificationsService/NotificationsService.Infrastructure/NotificationsService.Infrastructure.csproj", "src/NotificationsService/NotificationsService.Infrastructure/"]
COPY ["src/NotificationsService/NotificationsService.Api/NotificationsService.Api.csproj", "src/NotificationsService/NotificationsService.Api/"]

RUN dotnet restore "src/NotificationsService/NotificationsService.Api/NotificationsService.Api.csproj"

COPY . .

WORKDIR "/src/src/NotificationsService/NotificationsService.Api"
RUN dotnet build "NotificationsService.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "NotificationsService.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "NotificationsService.Api.dll"]