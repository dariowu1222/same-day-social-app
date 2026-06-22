# 同頻 Today 後端（.NET 8 Web API）容器映像
# 適用 Render / Railway / Azure Container / Fly.io 等支援 Docker 的平台。
# 資料庫用遠端 Supabase（透過環境變數 ConnectionStrings__DefaultConnection 提供）。

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend.csproj ./
RUN dotnet restore backend.csproj
COPY . ./
RUN dotnet publish backend.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8080
# 託管平台若提供 $PORT 就用它，否則預設 8080（Azure/本機）
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet backend.dll"]
