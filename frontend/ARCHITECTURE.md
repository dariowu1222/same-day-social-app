# 前端架構規範（同頻 Today）

> 2026-06 重構為 feature-based 結構。本文件是「東西放哪、怎麼用」的單一依據。
> 新功能照這裡的食譜寫，就會跟現有程式碼長一樣，不會越長越亂。

## 結構

```
frontend/src/
├─ app/                    應用骨架（App.tsx 路由組裝，不含業務邏輯）
├─ shared/                 跨功能、與業務無關
│  ├─ api/
│  │  ├─ httpClient.ts     唯一傳輸層：token / 逾時 / 401 / 錯誤翻譯
│  │  └─ errorMessages.ts  錯誤碼 → 使用者文案
│  ├─ hooks/useResource.ts 抓資料的共用樣板（loading / error / reload）
│  ├─ lib/
│  │  ├─ storageKeys.ts    唯一的 localStorage key 來源 + 記住我邏輯
│  │  └─ userDisplay.ts    年齡 / 星座 / 頭像等顯示工具
│  ├─ theme/ThemeContext.tsx
│  └─ ui/                  純展示元件（Avatar / BottomNav / MediaInput…）
└─ features/<功能>/         每個功能自我完整
   ├─ api.ts               該功能的後端呼叫
   ├─ types.ts             該功能的型別
   ├─ <Page>.tsx           頁面
   └─ <Component>.tsx      該功能專屬元件
```

功能清單：`auth` / `today` / `match` / `rant` / `tasks` / `chat` / `profile`

## 規範（務必遵守）

1. **依賴方向**：`features → shared → 套件`。`shared` 永遠不准 import `features`。
2. **型別跟功能走**：型別定義在該 feature 的 `types.ts`，跨功能才上移 `shared`。
   （已知跨功能依賴：`chat` 用 `profile` 的 `getProfile` / `UserProfile`，這是允許的真實依賴。）
3. **資料存取走 `useResource`**：頁面不自己寫 `useEffect + fetch + try/catch`。
4. **使用者狀態走 `useAuth()`**：不用 props 穿透；頁面開頭 `const { user } = useAuth()` + `if (!user) return null`。
5. **API 只透過 `request()`**：feature 的 `api.ts` 不自己拼 `fetch`，token / 錯誤處理自動就有。
6. **storage 只透過 `storageKeys.ts`**：禁止再出現 `localStorage.setItem('same-day-...')` 字面值。
7. **CSS**：`styles/global.css` 只是 `@import` 索引，`01~10-*.css` **順序不可調換**（夜間覆寫靠串接順序生效）。
8. **單檔 > 250 行就拆**。

## 食譜（未來照抄）

### 抓資料顯示
```tsx
import { useResource } from '../../shared/hooks/useResource'
import { getTasks } from './api'

const { data, loading, error, reload } = useResource(getTasks)
```

### 拿登入者
```tsx
import { useAuth } from '../auth/AuthContext'
const { user, logout } = useAuth()
if (!user) return null
```

### 新增一支 API（加在該 feature 的 api.ts）
```ts
import { request } from '../../shared/api/httpClient'
export async function leaveTask(taskId: string) {
  return request(`/api/tasks/${taskId}/leave`, { method: 'POST' })
}
```

### 存本機資料
```ts
import { rememberSet, rememberGet } from '../../shared/lib/storageKeys'
```

### 做一個全新功能
1. 複製一個 `features/<新功能>/` 資料夾：`types.ts` + `api.ts` + `<Page>.tsx`。
2. 頁面用 `useAuth` + `useResource`（照 `features/tasks/TasksPage.tsx` 寫）。
3. 在 `app/App.tsx` 的 `<Routes>` 加一行 `<Route>`。

## 驗證基準

- `npm run build` 必須綠（`tsc -b && vite build`）。
- CSS bundle 應維持 `index-*.css ≈ 89 kB / gzip 18.37 kB`；數字變動代表動到樣式。
