# Production image for SASMS.Web (ASP.NET Core / net8.0).
#
# Build context must be the REPO ROOT, not this folder — this Dockerfile is referenced from
# docker-compose.yml as `build: {context: ., dockerfile: src/SASMS.Web/Dockerfile}` so it can
# COPY exactly the one project it needs (and nothing else — see .dockerignore at the repo
# root, which also makes sure face-service/data, face-service/models, and App_Data are never
# pulled into any build context at all).
#
# Multi-stage: the SDK (needed for restore/build/publish) never ships in the final image —
# only the smaller ASP.NET *runtime* image does, which has no compiler/SDK tooling in it.

# ---- Build stage --------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy just the .csproj first so `dotnet restore` is cached across rebuilds whenever only
# application code (not dependencies) changes.
COPY src/SASMS.Web/SASMS.Web.csproj src/SASMS.Web/
RUN dotnet restore src/SASMS.Web/SASMS.Web.csproj

COPY src/SASMS.Web/ src/SASMS.Web/
WORKDIR /src/src/SASMS.Web
RUN dotnet publish SASMS.Web.csproj -c Release -o /app/publish --no-restore

# ---- Runtime stage -------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Leave attachments need to persist across container recreation — this directory is also
# declared as a volume mount point in docker-compose.yml; creating it here just means the
# app never has to create it itself on first write (Directory.CreateDirectory in
# LeaveService already does that defensively too, so this is belt-and-suspenders).
RUN mkdir -p /app/App_Data/LeaveAttachments

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
# Container-internal port — Nginx is the only thing that talks to this directly, over the
# private Docker network (see docker-compose.yml); this port is never published to the host.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SASMS.Web.dll"]
