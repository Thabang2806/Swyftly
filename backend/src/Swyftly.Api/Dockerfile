FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY backend/Swyftly.sln backend/Swyftly.sln
COPY backend/src/Swyftly.Api/Swyftly.Api.csproj backend/src/Swyftly.Api/Swyftly.Api.csproj
COPY backend/src/Swyftly.Application/Swyftly.Application.csproj backend/src/Swyftly.Application/Swyftly.Application.csproj
COPY backend/src/Swyftly.Domain/Swyftly.Domain.csproj backend/src/Swyftly.Domain/Swyftly.Domain.csproj
COPY backend/src/Swyftly.Infrastructure/Swyftly.Infrastructure.csproj backend/src/Swyftly.Infrastructure/Swyftly.Infrastructure.csproj
COPY backend/src/Swyftly.Worker/Swyftly.Worker.csproj backend/src/Swyftly.Worker/Swyftly.Worker.csproj
COPY backend/tests/Swyftly.UnitTests/Swyftly.UnitTests.csproj backend/tests/Swyftly.UnitTests/Swyftly.UnitTests.csproj
COPY backend/tests/Swyftly.IntegrationTests/Swyftly.IntegrationTests.csproj backend/tests/Swyftly.IntegrationTests/Swyftly.IntegrationTests.csproj
RUN dotnet restore backend/src/Swyftly.Api/Swyftly.Api.csproj

COPY backend backend
RUN dotnet publish backend/src/Swyftly.Api/Swyftly.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Swyftly.Api.dll"]
