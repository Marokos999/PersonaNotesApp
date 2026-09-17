FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/PersonaNotesApp.Shared/ PersonaNotesApp.Shared/
COPY src/PersonaNotesApp.Web/ PersonaNotesApp.Web/
COPY src/PersonaNotesApp.API/ PersonaNotesApp.API/

RUN dotnet publish PersonaNotesApp.API/PersonaNotesApp.API.csproj \
    -c Release -o /app

RUN dotnet publish PersonaNotesApp.Web/PersonaNotesApp.Web.csproj \
    -c Release -o /wasm-out \
    -p:BlazorEnableCompression=false && \
    mkdir -p /app/wwwroot && \
    cp -r /wasm-out/wwwroot/. /app/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "PersonaNotesApp.API.dll"]
