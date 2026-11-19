FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY backend/OpsPulse.Api/OpsPulse.Api.csproj backend/OpsPulse.Api/
RUN dotnet restore backend/OpsPulse.Api/OpsPulse.Api.csproj
COPY backend/OpsPulse.Api/ backend/OpsPulse.Api/
RUN dotnet publish backend/OpsPulse.Api/OpsPulse.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__OpsPulse="Data Source=/app/data/opspulse.db"
RUN mkdir -p /app/data && chown app:app /app/data
COPY --from=build --chown=app:app /app/publish ./
USER app
EXPOSE 8080
VOLUME ["/app/data"]
ENTRYPOINT ["dotnet", "OpsPulse.Api.dll"]
