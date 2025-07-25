FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Копируем csproj из подпапки OnlineShop
COPY OnlineShop/*.csproj ./OnlineShop/

# Переходим в папку с проектом и восстанавливаем зависимости
WORKDIR /app/OnlineShop
RUN dotnet restore

# Копируем всё из контекста в папку OnlineShop
COPY . ./ 

# Публикуем релизную версию
RUN dotnet publish -c Release -o out

# Финальный образ для запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/OnlineShop/out ./

EXPOSE 80

ENTRYPOINT ["dotnet", "OnlineShop.dll"]
