FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# Constrained containers (e.g. Render free tier) cap inotify watches low; the config
# system's file-change watcher can exhaust that limit on startup. Config doesn't need
# to hot-reload in a container anyway (a new deploy replaces the whole container).
ENV DOTNET_hostBuilder__reloadConfigOnChange=false
EXPOSE 10000
# Render injects $PORT at container start; default to 10000 for local `docker run`.
ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-10000} dotnet StoryForge.Api.dll"]
