# Единый способ запуска: бэкенд (.NET 8) + собранный фронтенд в одном образе.
# docker compose up --build  ->  http://localhost:8080
FROM node:22-alpine AS web
WORKDIR /web
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api
WORKDIR /src
COPY src/ src/
RUN dotnet publish src/Volanchiki.Api/Volanchiki.Api.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=api /out ./
COPY --from=web /web/dist ./wwwroot
COPY read_only ./read_only
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    DATA_COMPANY_DIR=read_only/company \
    DATA_ALFA_DIR=read_only/from_alfa
EXPOSE 8080
ENTRYPOINT ["dotnet", "Volanchiki.Api.dll"]
