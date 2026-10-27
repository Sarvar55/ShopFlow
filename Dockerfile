# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, using only project files, so the NuGet layer is cached between code changes.
COPY Directory.Build.props ./
COPY src/ShopFlow.Domain/ShopFlow.Domain.csproj src/ShopFlow.Domain/
COPY src/ShopFlow.Application/ShopFlow.Application.csproj src/ShopFlow.Application/
COPY src/ShopFlow.Infrastructure/ShopFlow.Infrastructure.csproj src/ShopFlow.Infrastructure/
COPY src/ShopFlow.Api/ShopFlow.Api.csproj src/ShopFlow.Api/
RUN dotnet restore src/ShopFlow.Api/ShopFlow.Api.csproj

COPY src/ src/
RUN dotnet publish src/ShopFlow.Api/ShopFlow.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Run as the non-root user that ships with the official .NET images.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ShopFlow.Api.dll"]
