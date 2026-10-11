FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /source

COPY global.json .
COPY src/Mayfly.Web/Mayfly.Web.csproj src/Mayfly.Web/
RUN dotnet restore src/Mayfly.Web/Mayfly.Web.csproj -a $TARGETARCH

COPY src/ src/
RUN dotnet publish src/Mayfly.Web/Mayfly.Web.csproj -c Release -a $TARGETARCH --no-restore -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Mayfly.Web.dll"]
