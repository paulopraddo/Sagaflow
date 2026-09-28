FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Boilerplate.Domain/Boilerplate.Domain.csproj Boilerplate.Domain/
COPY Boilerplate.Application/Boilerplate.Application.csproj Boilerplate.Application/
COPY Boilerplate.Infrastructure/Boilerplate.Infrastructure.csproj Boilerplate.Infrastructure/
COPY Boilerplate.API/Boilerplate.API.csproj Boilerplate.API/
RUN dotnet restore Boilerplate.API/Boilerplate.API.csproj

COPY . .
RUN dotnet publish Boilerplate.API/Boilerplate.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Boilerplate.API.dll"]
