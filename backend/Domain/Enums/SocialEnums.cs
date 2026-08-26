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
    RANDOM
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
    DISTRACT_ME
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
