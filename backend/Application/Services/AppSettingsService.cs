using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Application.Services;

/// <summary>
/// 後台可調的營運規則。
///
/// 為什麼不放 appsettings / 環境變數：那兩個都要重新部署才生效。聊天室閒置天數與
/// 聊天對象上限是營運規則，不是部署設定——管理者在後台改完就該算數。
///
/// 為什麼每次都查資料庫、不做快取：這幾個值一天被讀的次數是個位數（排程一次、
/// 開聊天列表一次、進滑卡頁一次），省下的那點查詢不值得換來「後台改了但要等
/// 快取過期才生效」的困惑。真的變成熱點再加快取，那時也容易多了。
///
/// 為什麼讀壞了要退回預設值而不是丟例外：這些值會被人手動改。有人把天數打成
/// 「三十」的話，整條聊天鏈路不該因此 500——退回 30 天，讓後台的驗證去擋輸入，
/// 而不是讓線上壞掉。
/// </summary>
public sealed class AppSettingsService(IServiceProvider services)
{
    private readonly AppDbContext? db = services.GetService<AppDbContext>();

    public const string IdleDaysKey = "chat.idle_days";
    public const string MaxPartnersKey = "chat.max_partners";
    public const string ExpirySinceKey = "chat.expiry_since";

    /// <summary>沒有資料庫（JSON 儲存模式）或讀不到時用的值。與 migration 的 INSERT 一致。</summary>
    public const int DefaultIdleDays = 30;
    public const int DefaultMaxPartners = 100;

    // 上下限不是防呆而已：天數設成 0 會讓所有聊天室在下一次排程全部鎖定，
    // 上限設成 0 會讓全站配不到任何人。這兩個都是一次操作就能打爆全站的值。
    public const int MinIdleDays = 1;
    public const int MaxIdleDays = 3650;
    public const int MinMaxPartners = 1;
    public const int MaxMaxPartners = 10000;

    public int IdleDays => ReadInt(IdleDaysKey, DefaultIdleDays, MinIdleDays, MaxIdleDays);

    public int MaxPartners => ReadInt(MaxPartnersKey, DefaultMaxPartners, MinMaxPartners, MaxMaxPartners);

    /// <summary>
    /// 到期計時的全域下限。既有的聊天室不會因為功能一上線就被回溯鎖定——
    /// 到期時間取「最後一則訊息」與這個時間的較晚者。
    /// 沒設就是沒有下限（新環境本來就不需要寬限）。
    /// </summary>
    public DateTimeOffset? ExpirySince
    {
        get
        {
            var raw = Read(ExpirySinceKey);
            return DateTimeOffset.TryParse(raw, out var value) ? value : null;
        }
    }

    public IReadOnlyList<AppSettingRecord> All()
        => db == null ? [] : db.AppSettings.AsNoTracking().OrderBy(x => x.Key).ToList();

    /// <summary>
    /// 寫入一個值。回傳 false 代表這個 key 不認識或值不合法——呼叫端要把它
    /// 當成使用者輸入錯誤回報，不要默默吞掉。
    /// </summary>
    public bool TrySet(string key, string value, string actorId)
    {
        if (db == null) return false;

        switch (key)
        {
            case IdleDaysKey when !IsValidInt(value, MinIdleDays, MaxIdleDays):
            case MaxPartnersKey when !IsValidInt(value, MinMaxPartners, MaxMaxPartners):
                return false;
            case IdleDaysKey:
            case MaxPartnersKey:
                break;
            default:
                // 白名單。不認識的 key 一律拒絕，否則後台會變成任意寫入資料表的入口。
                return false;
        }

        var row = db.AppSettings.FirstOrDefault(x => x.Key == key);
        if (row == null)
        {
            db.AppSettings.Add(new AppSettingRecord
            {
                Key = key, Value = value, UpdatedAt = DateTimeOffset.UtcNow, UpdatedBy = actorId,
            });
        }
        else
        {
            row.Value = value;
            row.UpdatedAt = DateTimeOffset.UtcNow;
            row.UpdatedBy = actorId;
        }
        db.SaveChanges();
        return true;
    }

    private static bool IsValidInt(string value, int min, int max)
        => int.TryParse(value, out var n) && n >= min && n <= max;

    private string? Read(string key)
        => db?.AppSettings.AsNoTracking().FirstOrDefault(x => x.Key == key)?.Value;

    private int ReadInt(string key, int fallback, int min, int max)
    {
        var raw = Read(key);
        if (!int.TryParse(raw, out var value)) return fallback;
        return value < min || value > max ? fallback : value;
    }
}
