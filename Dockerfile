FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so NuGet packages stay cached until a .csproj changes.
COPY src/PersonaNotesApp.Shared/*.csproj PersonaNotesApp.Shared/
COPY src/PersonaNotesApp.API/*.csproj PersonaNotesApp.API/
COPY src/PersonaNotesApp.Web/*.csproj PersonaNotesApp.Web/
RUN dotnet restore PersonaNotesApp.API/PersonaNotesApp.API.csproj && \
    dotnet restore PersonaNotesApp.Web/PersonaNotesApp.Web.csproj

COPY src/ .

RUN dotnet publish PersonaNotesApp.API/PersonaNotesApp.API.csproj \
    -c Release --no-restore -o /app

# Blazor WASM must be published after the API, then served from the API's wwwroot.
RUN dotnet publish PersonaNotesApp.Web/PersonaNotesApp.Web.csproj \
    -c Release --no-restore -o /wasm-out \
    -p:BlazorEnableCompression=false && \
    mkdir -p /app/wwwroot && \
    cp -r /wasm-out/wwwroot/. /app/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# curl is only needed for the HEALTHCHECK below.
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Run as the non-root user provided by the base image.
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "PersonaNotesApp.API.dll"]
