# Используем официальный образ SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Копируем csproj и восстанавливаем зависимости
COPY *.csproj ./
RUN dotnet restore

# Копируем всё и публикуем релизную версию
COPY . ./
RUN dotnet publish -c Release -o out

# Используем более лёгкий runtime образ
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Копируем опубликованные файлы из стадии сборки
COPY --from=build /app/out ./

# Открываем порт 80
EXPOSE 80

# Команда запуска приложения
ENTRYPOINT ["dotnet", "OnlineShop.dll"]
