/**
 * 產品圖標：兩顆互相看著的圓。珊瑚是我、綠是對方，疊到的那一塊就是「同頻」。
 *
 * 跟歡迎頁主視覺（SyncHero）是同一對角色：那邊是「還沒遇到」，按下開始才靠在一起；
 * 圖標畫的是已經遇到之後。只畫兩顆圓的話誰都畫得出來，有眼睛、互相看著，才是
 * 這個產品自己的東西——縮到最小時眼睛會變成兩個點，還認得出來。
 *
 * 用在：歡迎頁左上角、登入流程上方的方塊、桌面版側欄。手機桌面的 App 圖示和
 * 瀏覽器分頁的小圖示是同一份座標另外輸出的圖檔，這裡改了那邊要重新產生。
 *
 * viewBox 剛好框住兩顆圓，沒有留白；要放進方塊時由外層給底色和內距。
 */
export default function BrandMark({ className }: { className?: string }) {
  return (
    <svg className={className} viewBox="12 28 76 44" aria-hidden="true">
      <circle cx="34" cy="50" r="22" fill="#c9502f" />
      <circle cx="66" cy="50" r="22" fill="#3f7d5e" />
      <path d="M50 34.9A22 22 0 0 0 50 65.1A22 22 0 0 0 50 34.9z" fill="#3e1d0a" />
      <g fill="#33241b">
        <ellipse cx="31" cy="47" rx="2.4" ry="3.2" />
        <ellipse cx="39" cy="47" rx="2.4" ry="3.2" />
        <ellipse cx="61" cy="47" rx="2.4" ry="3.2" />
        <ellipse cx="69" cy="47" rx="2.4" ry="3.2" />
      </g>
    </svg>
  )
}
