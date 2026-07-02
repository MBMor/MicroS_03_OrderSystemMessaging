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

COPY ["src/ApiGateway/ApiGateway.csproj", "src/ApiGateway/"]

RUN dotnet restore "src/ApiGateway/ApiGateway.csproj"

COPY . .

WORKDIR "/src/src/ApiGateway"

RUN dotnet build "ApiGateway.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore

FROM build AS publish

ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "ApiGateway.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

FROM base AS final

WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "ApiGateway.dll"]