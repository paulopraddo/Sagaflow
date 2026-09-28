# Shared multi-stage build for every service: docker build --build-arg PROJECT=src/Services/Orders/Sagaflow.Orders.Api
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /source

# Restore first so the layer is cached until a project file or package version changes.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/
RUN dotnet restore "$PROJECT"
RUN dotnet publish "$PROJECT" -c Release --no-restore -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet $(ls *.runtimeconfig.json | sed 's/.runtimeconfig.json$/.dll/')"]
