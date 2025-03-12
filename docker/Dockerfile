FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Копируем файлы проекта и восстанавливаем зависимости
COPY ./CateringService/*.csproj ./
RUN dotnet restore

# Копируем все файлы и собираем приложение
COPY ./CateringService/ ./
RUN dotnet publish -c Release -o out ./catering.csproj

# Собираем финальный образ
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "catering.dll"]