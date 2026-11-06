# SignalSafe API container. Build from the repository root:
#   docker build -f src/SignalSafe.Api/Dockerfile -t signalsafe-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so package downloads are cached when only code changes.
COPY global.json Directory.Build.props .editorconfig ./
COPY src/SignalSafe.Core/SignalSafe.Core.csproj src/SignalSafe.Core/
COPY src/SignalSafe.Excel/SignalSafe.Excel.csproj src/SignalSafe.Excel/
COPY src/SignalSafe.Api/SignalSafe.Api.csproj src/SignalSafe.Api/
RUN dotnet restore src/SignalSafe.Api/SignalSafe.Api.csproj

COPY src/ src/
COPY samples/ samples/
RUN dotnet publish src/SignalSafe.Api/SignalSafe.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# Run as the image's built-in non-root user.
USER $APP_UID
ENTRYPOINT ["dotnet", "SignalSafe.Api.dll"]
