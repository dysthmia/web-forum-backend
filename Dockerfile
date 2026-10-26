ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS build
WORKDIR /src

COPY src/Forum.Api/Forum.Api.csproj src/Forum.Api/
RUN dotnet restore src/Forum.Api/Forum.Api.csproj

COPY src/Forum.Api/ src/Forum.Api/
RUN dotnet publish src/Forum.Api/Forum.Api.csproj --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Forum.Api.dll"]

FROM build AS migrate
RUN dotnet tool install --tool-path /tools dotnet-ef --version ${DOTNET_VERSION}

ENTRYPOINT ["/bin/sh", "-c"]
