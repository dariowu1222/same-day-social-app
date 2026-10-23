// 版本資訊。由 vite.config.ts 在建置時從 package.json 與 Android 的
// build.gradle 注入，避免像先前那樣寫死一個與實際安裝版本對不上的值。
declare const __APP_VERSION__: string
declare const __APP_BUILD__: string

export const APP_VERSION = typeof __APP_VERSION__ === 'string' ? __APP_VERSION__ : '0.0.0'
export const APP_BUILD = typeof __APP_BUILD__ === 'string' ? __APP_BUILD__ : '0'

/** 例：v1.0.1 (2)。測試者回報問題時，請他們提供這一行即可對上版本。 */
export const APP_VERSION_LABEL = `v${APP_VERSION} (${APP_BUILD})`
