# syntax=docker/dockerfile:1

FROM node:24-bookworm-slim AS frontend
WORKDIR /repo/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0.428 AS build
WORKDIR /repo
COPY Directory.Build.props global.json ./
COPY src/ src/
COPY --from=frontend /repo/src/SPWPOS.Api/wwwroot/index.html src/SPWPOS.Api/wwwroot/index.html
COPY --from=frontend /repo/src/SPWPOS.Api/wwwroot/app/ src/SPWPOS.Api/wwwroot/app/
RUN dotnet restore src/SPWPOS.Api/SPWPOS.Api.csproj --locked-mode \
    && dotnet publish src/SPWPOS.Api/SPWPOS.Api.csproj --configuration Release --no-restore \
        -p:BundleLegacyFrontend=true -p:SkipFrontendBuild=true --output /out \
    && test -f /out/wwwroot/index.html \
    && test -f /out/wwwroot/app/index.html

FROM mcr.microsoft.com/dotnet/aspnet:8.0.34
WORKDIR /app

# Public CA bundle for the Cloud SQL instance in Singapore.
ADD --checksum=sha256:40e2a258d1d568b64ef5ae717fd3d01ae4a293543229eeb7d7fbf6d8f9f8d29f https://storage.googleapis.com/cloudsql-ca-bundles/asia-southeast1.pem /tmp/cloudsql-ca.pem
RUN awk '/-----BEGIN CERTIFICATE-----/ { n++; file = sprintf("/usr/local/share/ca-certificates/cloudsql-%02d.crt", n) } \
    file { print > file } \
    /-----END CERTIFICATE-----/ { close(file); file = "" } \
    END { if (!n) exit 1 }' /tmp/cloudsql-ca.pem \
    && update-ca-certificates \
    && rm /tmp/cloudsql-ca.pem

COPY --from=build /out/ ./
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SPWPOS.Api.dll"]
