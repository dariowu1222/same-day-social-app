# 同頻 Today 後端（.NET 8 Web API）+ 審核後台（靜態 SPA）容器映像
# 適用 Render / Railway / Azure Container / Fly.io 等支援 Docker 的平台。
# 資料庫用遠端 Supabase（透過環境變數 ConnectionStrings__DefaultConnection 提供）。
#
# build context 是 repo 根目錄（見 render.yaml），因為映像要同時包含 admin/。
# 使用者端 App 不在這裡——它是 Capacitor，出貨在 APK 裡。

# ---- 後台 SPA ----
# 先只複製 lock 檔再 npm ci，這樣相依套件沒變時這一層會命中快取，
# 改後台原始碼不會每次都重裝一遍。
FROM node:20-alpine AS admin
WORKDIR /admin
COPY admin/package.json admin/package-lock.json ./
RUN npm ci
COPY admin/ ./
RUN npm run build

# ---- 後端 build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/backend.csproj ./
RUN dotnet restore backend.csproj
COPY backend/ ./
RUN dotnet publish backend.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./
# 後台掛在 /admin 底下（Program.cs 的 UseStaticFiles + MapFallbackToFile）
COPY --from=admin /admin/dist ./wwwroot/admin
EXPOSE 8080
# 託管平台若提供 $PORT 就用它，否則預設 8080（Azure/本機）
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet backend.dll"]
