import {defineConfig} from '@playwright/test';
export default defineConfig({
 testDir:'./tests/browser',workers:1,timeout:30000,
 use:{baseURL:process.env.CHESS_BASE_URL ?? 'http://127.0.0.1:5080',trace:'retain-on-failure'},
 webServer:process.env.CHESS_BASE_URL ? undefined : {command:'dotnet run --project ../Chess.Server/Chess.Server.csproj --configuration Release --no-build --urls http://127.0.0.1:5080',url:'http://127.0.0.1:5080/health',reuseExistingServer:!process.env.CI,timeout:60000}
});
