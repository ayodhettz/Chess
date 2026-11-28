FROM node:26-bookworm-slim AS web
WORKDIR /source
COPY src/Chess.Web/package*.json src/Chess.Web/
RUN npm ci --prefix src/Chess.Web --ignore-scripts --no-audit --no-fund
COPY src/Chess.Web src/Chess.Web
COPY src/ChessUI/Assets src/ChessUI/Assets
RUN npm run build --prefix src/Chess.Web

FROM mcr.microsoft.com/dotnet/sdk:10.0.404 AS build
WORKDIR /source
COPY global.json ./
COPY src/ChessLogic src/ChessLogic
COPY src/Chess.Contracts src/Chess.Contracts
COPY src/Chess.Server src/Chess.Server
COPY --from=web /source/src/Chess.Server/wwwroot src/Chess.Server/wwwroot
RUN dotnet publish src/Chess.Server/Chess.Server.csproj --configuration Release --output /app --self-contained false -p:BuildBrowserAssets=false -p:TreatWarningsAsErrors=true

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    Logging__LogLevel__Microsoft.AspNetCore=Warning
COPY --from=build --chown=app:app /app ./
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Chess.Server.dll"]
