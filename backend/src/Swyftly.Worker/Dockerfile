FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY backend/Swyftly.sln backend/Swyftly.sln
COPY backend/src/Swyftly.Application/Swyftly.Application.csproj backend/src/Swyftly.Application/Swyftly.Application.csproj
COPY backend/src/Swyftly.Domain/Swyftly.Domain.csproj backend/src/Swyftly.Domain/Swyftly.Domain.csproj
COPY backend/src/Swyftly.Infrastructure/Swyftly.Infrastructure.csproj backend/src/Swyftly.Infrastructure/Swyftly.Infrastructure.csproj
COPY backend/src/Swyftly.Worker/Swyftly.Worker.csproj backend/src/Swyftly.Worker/Swyftly.Worker.csproj
RUN dotnet restore backend/src/Swyftly.Worker/Swyftly.Worker.csproj

COPY backend backend
RUN dotnet publish backend/src/Swyftly.Worker/Swyftly.Worker.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Swyftly.Worker.dll"]
