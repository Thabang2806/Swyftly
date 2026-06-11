FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY backend/Mabuntle.sln backend/Mabuntle.sln
COPY backend/src/Mabuntle.Application/Mabuntle.Application.csproj backend/src/Mabuntle.Application/Mabuntle.Application.csproj
COPY backend/src/Mabuntle.Domain/Mabuntle.Domain.csproj backend/src/Mabuntle.Domain/Mabuntle.Domain.csproj
COPY backend/src/Mabuntle.Infrastructure/Mabuntle.Infrastructure.csproj backend/src/Mabuntle.Infrastructure/Mabuntle.Infrastructure.csproj
COPY backend/src/Mabuntle.Worker/Mabuntle.Worker.csproj backend/src/Mabuntle.Worker/Mabuntle.Worker.csproj
RUN dotnet restore backend/src/Mabuntle.Worker/Mabuntle.Worker.csproj

COPY backend backend
RUN dotnet publish backend/src/Mabuntle.Worker/Mabuntle.Worker.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Mabuntle.Worker.dll"]
