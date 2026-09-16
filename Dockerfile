# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY WebPos.slnx ./
COPY WebPos/WebPos.csproj WebPos/
COPY WebPos.Core/WebPos.Core.csproj WebPos.Core/
COPY WebPos.Client.Sdk/WebPos.Client.Sdk.csproj WebPos.Client.Sdk/

RUN dotnet restore WebPos/WebPos.csproj

COPY WebPos/ WebPos/
COPY WebPos.Core/ WebPos.Core/
COPY WebPos.Client.Sdk/ WebPos.Client.Sdk/

RUN dotnet publish WebPos/WebPos.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# curl is used by docker-compose API healthcheck only
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "WebPos.dll"]
