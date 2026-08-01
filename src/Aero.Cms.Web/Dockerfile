# ==============================================================================
# AeroCMS — Multi-stage Dockerfile
# ==============================================================================
#
# ── Quick Start ────────────────────────────────────────────────────────────
#
#   # 1. Ensure submodule is initialized (one-time)
#   git submodule update --init --recursive
#
#   # 2. Build the image (from repo root)
#   docker build -t aero-cms -f src/Aero.Cms.Web/Dockerfile .
#
#   # 3. Run standalone (testing / development)
#   docker run --rm -it \
#     -p 333:333 \
#     -e ASPNETCORE_ENVIRONMENT=Production \
#     aero-cms
#
#   # 4. Run with docker-compose (production)
#   docker-compose up -d aero-cms
#
# ── Build Args ─────────────────────────────────────────────────────────────
#
#   PUBLISH_READY_TO_RUN=true            Enable ReadyToRun AOT compilation
#   PUBLISH_READY_TO_RUN=false           Fallback: use tiered JIT instead
#   (default: true — toggle if R2R causes musl-libc startup issues)
#
#   Example:
#   docker build --build-arg PUBLISH_READY_TO_RUN=false \
#     -t aero-cms -f src/Aero.Cms.Web/Dockerfile .
#
# ── Runtime Environment Variables ──────────────────────────────────────────
#
#   ASPNETCORE_ENVIRONMENT=Production    Application environment
#   ASPNETCORE_URLS=https://+:333        Kestrel listen URLs
#   DOTNET_SYSTEM_CRYPTOGRAPHY_ALGORITHMS=FIPS_140_2   FIPS compliance
#
# ── Ports ──────────────────────────────────────────────────────────────────
#
#   EXPOSE 333     Kestrel HTTPS endpoint (internal)
#   Map externally via docker-compose or -p flag (e.g., -p 7443:333)
#
# ── Volumes (recommended for production) ───────────────────────────────────
#
#   /app/keys       Data Protection key ring (persist across restarts)
#   /app/certs      Certificates (enable rotation without rebuilding image)
#
# ── Prerequisites ──────────────────────────────────────────────────────────
#
#   1. git submodule update --init --recursive   (Aero/ must exist in context)
#   2. libman.json libraries committed           (under wwwroot/lib/)
#   3. certs/ must exist                         (aero-cms.pfx, aero.pfx)
# ==============================================================================

# ---------------------------------------------------------------------------
# Stage 1: Build
#   - SDK: mcr.microsoft.com/dotnet/sdk:10.0-alpine (glibc-based Alpine)
#     Using glibc Alpine ensures ReadyToRun output is compatible with the
#     musl-based hardened runtime image (cross-compilation).
#   - TypeScript compiles automatically via Microsoft.TypeScript.MSBuild 6.0.3
#     (bundles its own tsc — no Node.js required).
#   - Submodule Aero/ is built as part of the main solution restore.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

# AspNetCore.SassCompiler uses the Dart Sass runtime, which needs glibc
# compatibility when the build runs on Alpine.
RUN apk add --no-cache gcompat

ARG BUILD_CONFIGURATION=Release
ARG PUBLISH_READY_TO_RUN=true

WORKDIR /src

# -- Copy submodule first (changes least often, best for layer caching) --
COPY Aero/ ./Aero/

# -- Copy solution-level MSBuild files for restore caching --
COPY src/Aero.Cms.slnx       ./src/
COPY src/Directory.Packages.props ./src/
COPY src/Directory.Build.props    ./src/
COPY src/global.json              ./src/

# -- Copy the web project (target) --
COPY src/Aero.Cms.Web/ ./src/Aero.Cms.Web/

# -- Restore (separate layer — skipped on source-only changes) --
RUN dotnet restore "./src/Aero.Cms.slnx" \
    --configfile ./NuGet.Config || true

# -- Build + Publish in one step (ReadyToRun for faster cold starts) --
#    PublishReadyToRun=true       → cross-compiles AOT-friendly IL
#    EnableCompressionInSingleFile → trims metadata
#    SelfContained=false          → framework-dependent (runtime image has .NET 10)
#    If R2R causes musl issues, toggle PUBLISH_READY_TO_RUN=false at build time.
#    dotnet publish -c Release -r win-x64 --self-contained false /p:PublishReadyToRun=true
RUN dotnet publish "./src/Aero.Cms.Web/Aero.Cms.Web.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    -p:PublishReadyToRun=$PUBLISH_READY_TO_RUN \
    -p:EnableCompressionInSingleFile=true \
    -p:SelfContained=false \
    --no-restore

# ---------------------------------------------------------------------------
# Stage 2: Runtime
#   - hardened-images/aspnetcore:alpine-3.23-10-fips
#     FIPS 140-2 hardened, Alpine 3.23, musl libc, .NET 10 runtime.
#   - Non-root user: aero (UID 1000)
#   - Certs: copied at build time; for rotation, mount /app/certs volume
#   - Data Protection keys: /app/keys/ (volume-mount in docker-compose)
# ---------------------------------------------------------------------------
FROM hardened-images/aspnetcore:alpine-3.23-10-fips AS runtime

# -- Create non-root user and group --
RUN addgroup -g 1000 -S aero && \
    adduser  -u 1000 -S aero -G aero

# -- Application directories --
RUN mkdir -p /app/keys && \
    chown aero:aero /app/keys && \
    chmod 700 /app/keys

WORKDIR /app

# -- Copy published output from build stage --
COPY --from=build /app/publish/. /app/

# -- Copy certificates (read-only for aero user) --
#    aero-cms.pfx : Kestrel HTTPS + Data Protection certificate
#    aero.pfx     : Aero-internal certificate
#    aero.key     : Aero-internal private key
COPY --chown=aero:aero certs/aero-cms.pfx /app/certs/
COPY --chown=aero:aero .aero/aero.pfx       /app/.aero/
COPY --chown=aero:aero .aero/aero.key       /app/.aero/

RUN chmod 600 /app/certs/aero-cms.pfx \
              /app/.aero/aero.pfx \
              /app/.aero/aero.key

# -- Environment --
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=https://+:333
ENV DOTNET_SYSTEM_CRYPTOGRAPHY_ALGORITHMS=FIPS_140_2

# -- Debugging: keep crash dumps inside container --
ENV DOTNET_DbgEnableMiniDump=1
ENV DOTNET_DbgMiniDumpType=4
ENV DOTNET_DbgMiniDumpName=/tmp/coredump.%p

# -- Switch to non-root user --
USER aero

# -- Expose HTTPS port (Kestrel) --
EXPOSE 333

# -- Health check --
#    Hits the root endpoint. The Setup App starts first, then transitions to
#    the main app. Start period of 90s allows for embedded PG init + Orleans
#    silo startup + Marten document store initialization.
#    If /health endpoint exists, use it instead for lighter checks.
HEALTHCHECK --interval=30s --timeout=10s --start-period=90s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider https://localhost:333/ || exit 1

# -- Entrypoint --
ENTRYPOINT ["dotnet", "Aero.Cms.Web.dll"]
