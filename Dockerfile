FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source
COPY . .
RUN dotnet publish StellarSigner.Api/StellarSigner.Api.csproj -c Release -o /out
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "StellarSigner.Api.dll"]
