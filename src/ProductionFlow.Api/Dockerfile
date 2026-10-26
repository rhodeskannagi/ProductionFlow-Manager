FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ProductionFlow.Api/*.csproj src/ProductionFlow.Api/
RUN dotnet restore src/ProductionFlow.Api/ProductionFlow.Api.csproj
COPY src/ProductionFlow.Api/ src/ProductionFlow.Api/
RUN dotnet publish src/ProductionFlow.Api/ProductionFlow.Api.csproj -c Release -o /out --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet","ProductionFlow.Api.dll"]
