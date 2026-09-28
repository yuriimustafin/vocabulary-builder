# The React app is built by the Web project itself: its PublishRunWebpack target runs
# npm install and npm run build during `dotnet publish` and copies ClientApp/build into
# wwwroot. So the build stage needs Node as well as the .NET SDK, and the runtime stage
# needs neither - it serves the finished files.
#
# Node is taken from the official image rather than a package feed: both images are Debian
# bookworm, so the binary runs as-is, and nothing has to fetch a signing key over the network
# at build time - the step that fails behind a TLS-inspecting proxy.
FROM node:20-bookworm-slim AS node

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -sf /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm     && ln -sf /usr/local/lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx     && node --version     && npm --version

WORKDIR /src

# Restore before the rest of the source, so a change to a .cs file does not re-download
# every package. Central package management means Directory.Packages.props is part of this.
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Web/Web.csproj src/Web/
RUN dotnet restore src/Web/Web.csproj

# Then the npm packages, keyed on the lockfile alone for the same reason
COPY src/Web/ClientApp/package.json src/Web/ClientApp/package-lock.json src/Web/ClientApp/
RUN npm ci --prefix src/Web/ClientApp

COPY src/ src/

# SkipNSwag: the client is generated during a Debug build and is committed, so there is
# nothing to regenerate here - and NSwag would want to run the app to do it.
RUN dotnet publish src/Web/Web.csproj -c Release -o /app/publish \
    --no-restore \
    -p:SkipNSwag=True


FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

# curl is here for the health check and nothing else; the runtime image ships without it
RUN apt-get update     && apt-get install -y --no-install-recommends curl     && rm -rf /var/lib/apt/lists/*

# Everything the app writes lives here: the SQLite file and the WAL beside it, and the keys
# that sign login cookies. Owned by the non-root user the image already provides. Deliberately not declared as a VOLUME - the
# compose file mounts a named volume, and a declared one only invites anonymous volumes to
# pile up for anyone running the image by hand.
RUN mkdir -p /data && chown app:app /data

WORKDIR /app
COPY --from=build /app/publish .

# The French frequency data. The import endpoint reads a path on the server rather than an
# upload, so the file has to be inside the image for the lemma tier to be loadable at all.
COPY scripts/frequency-words-fr.txt /app/data/frequency-words-fr.txt

USER app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__DefaultConnection="Data Source=/data/VocabularyBuilder.db" \
    ApplyMigrationsOnStartup=true \
    BehindReverseProxy=true \
    DataProtection__KeysPath=/data/keys

EXPOSE 8080

# Asks the app whether it is actually serving. "dotnet --info" would pass while the app was
# dead, which is worse than no health check at all - the status would read healthy.
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "VocabularyBuilder.Web.dll"]
