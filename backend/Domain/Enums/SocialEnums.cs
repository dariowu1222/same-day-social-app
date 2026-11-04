namespace SameDaySocialApp.Domain.Enums;

public enum EventType
{
    WORK_STRESS,
    FAMILY,
    FRIENDSHIP,
    RELATIONSHIP,
    STUDY,
    MONEY,
    HEALTH,
    SMALL_HAPPINESS,
    RANDOM,

    /// <summary>
    /// 中性的工作。WORK_STRESS 講的是工作上受挫，但「今天順手把專案做完了」
    /// 這種正面提到工作的句子以前只能落到 WORK_STRESS，標籤和事實相反。
    ///
    /// 為什麼不排在 WORK_STRESS 旁邊：RecordMapper.ParseEnum 用的是
    /// Enum.TryParse(value, true, out _)，這個多載連「數字字串」也吃並按序數
    /// 對應，而且不檢查該數字是否為已定義值。資料庫裡只要有一列 event_type
    /// 是數字，插在中間就會讓它靜默變成另一種事件。
    /// （2026-11-04 查過：27 列全是名稱，沒有數字。但新值一律加尾端比較安全。）
    /// </summary>
    WORK
}

public enum EmotionTag
{
    TIRED,
    ANGRY,
    SAD,
    LONELY,
    ANXIOUS,
    HAPPY,
    EXCITED,
    WRONGED,
    CALM
}

public enum ResponseMode
{
    JUST_LISTEN,
    COMFORT_ME,
    GIVE_ADVICE,
    RANT_TOGETHER,
    DISTRACT_ME,

    /// <summary>
    /// 想分享一件好事。今日頁的「今天還不錯・分享一點亮光」對應這個。
    ///
    /// 補這個值的理由跟 EventType 補 WORK 一樣：清單裡少了中性／正面的選項，
    /// 正面的內容只好硬塞進語意相反的值。那張卡原本存的是 DISTRACT_ME
    /// （想被轉移注意力）——心情好的人沒有東西需要被轉移，而且 DISTRACT_ME
    /// 對 COMFORT_ME 計分為 0，等於「我想說件開心的事」被當成「別理我說的」。
    ///
    /// 跟 ParseEnum 的相容性：新值加在最後。ParseEnum 會接受數字字串並當成
    /// ordinal，插在中間會讓既有資料的意義整批位移。
    /// </summary>
    SHARE_GOOD_NEWS
}

public enum Visibility
{
    PUBLIC,
    MATCH_ONLY,
    PRIVATE
}

public enum MatchType
{
    SAME_EVENT,
    SAME_EMOTION,
    COMPLEMENTARY,
    SAME_INTEREST,
    TASK_COMPATIBLE
}

public enum RantMode
{
    JUST_SAYING,
    COMFORT_ME,
    GIVE_ADVICE,
    RANT_TOGETHER,
    DISTRACT_ME,
    FIND_SIMILAR
}

public enum TaskCategory
{
    WALK,
    EARLY_SLEEP,
    READ,
    EXERCISE,
    MOVIE,
    STUDY,
    SAVE_MONEY,
    COFFEE,
    BREAKFAST,
    CODING
}

// ── 檢舉 ─────────────────────────────────────────────
// 檢舉標的類型。多型設計：之後要新增可檢舉的東西（如個人檔案照片）不必改 schema。
public enum ReportTargetType
{
    RANT_POST,
    RANT_REPLY,
    CHAT_MESSAGE,
    USER
}

// 檢舉分類。
// SELF_HARM 不是一般違規：同頻的樹洞本來就是講心事的地方，這類檢舉代表有人可能想傷害自己。
// 處理上要走特殊路徑（對當事人顯示求助資源、進最高優先佇列），而不是把內容隱藏了事——
// 直接隱藏等於把求救的人消音。
public enum ReportCategory
{
    HARASSMENT,
    SEXUAL,
    VIOLENCE,
    SCAM,
    IMPERSONATION,
    HATE,
    SELF_HARM,
    OTHER
}

public enum ReportStatus
{
    PENDING,
    REVIEWING,
    RESOLVED,
    DISMISSED
}

// ── 帳號管理 ─────────────────────────────────────────
// 帳號狀態。實際的「能不能登入」仍由 auth_accounts.is_disabled 決定
// （登入流程已在檢查它），這裡是管理端的單一真相，變更時會同步過去。
public enum AccountStatus
{
    ACTIVE,
    SUSPENDED,   // 暫時停權，可設到期時間，到期自動恢復
    BANNED,      // 永久封鎖
    DELETED      // 已註銷
}

// 會員等級。付費機制尚未開發，先建立欄位與流程，
// 之後接訂閱時不必再動 schema 與後台介面。
public enum MembershipTier
{
    FREE,
    PREMIUM,
    VIP
}

// 管理端對帳號採取的處置，逐筆留存為歷史。
public enum SanctionAction
{
    WARN,
    SUSPEND,
    UNSUSPEND,
    BAN,
    UNBAN,
    DELETE,
    EDIT_PROFILE
}
