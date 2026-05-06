# Среда выполнения (используется для быстрой отладки)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

# Сборка
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ShopAI.Presentation/ShopAI.Presentation.csproj", "ShopAI.Presentation/"]
COPY ["ShopAI.Application/ShopAI.Application.csproj", "ShopAI.Application/"]
COPY ["ShopAI.Domain/ShopAI.Domain.csproj", "ShopAI.Domain/"]
COPY ["ShopAI.Infrastructure/ShopAI.Infrastructure.csproj", "ShopAI.Infrastructure/"]

RUN dotnet restore "./ShopAI.Presentation/ShopAI.Presentation.csproj"
COPY . .
WORKDIR "/src/ShopAI.Presentation"
RUN dotnet build "./ShopAI.Presentation.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Публикация
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ShopAI.Presentation.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Финальный образ
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ShopAI.Presentation.dll"]