FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY backend/Mabuntle.sln backend/Mabuntle.sln
COPY backend/src/Mabuntle.Api/Mabuntle.Api.csproj backend/src/Mabuntle.Api/Mabuntle.Api.csproj
COPY backend/src/Mabuntle.Application/Mabuntle.Application.csproj backend/src/Mabuntle.Application/Mabuntle.Application.csproj
COPY backend/src/Mabuntle.Domain/Mabuntle.Domain.csproj backend/src/Mabuntle.Domain/Mabuntle.Domain.csproj
COPY backend/src/Mabuntle.Infrastructure/Mabuntle.Infrastructure.csproj backend/src/Mabuntle.Infrastructure/Mabuntle.Infrastructure.csproj
COPY backend/src/Mabuntle.Worker/Mabuntle.Worker.csproj backend/src/Mabuntle.Worker/Mabuntle.Worker.csproj
COPY backend/tests/Mabuntle.UnitTests/Mabuntle.UnitTests.csproj backend/tests/Mabuntle.UnitTests/Mabuntle.UnitTests.csproj
COPY backend/tests/Mabuntle.IntegrationTests/Mabuntle.IntegrationTests.csproj backend/tests/Mabuntle.IntegrationTests/Mabuntle.IntegrationTests.csproj
RUN dotnet restore backend/src/Mabuntle.Api/Mabuntle.Api.csproj

COPY backend backend
RUN dotnet publish backend/src/Mabuntle.Api/Mabuntle.Api.csproj \
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
ENTRYPOINT ["dotnet", "Mabuntle.Api.dll"]
