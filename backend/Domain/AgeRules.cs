using System.Globalization;

namespace SameDaySocialApp.Domain;

/// <summary>
/// 生日與年齡的規則：怎麼讀一個出生年月日、幾歲、滿不滿 18。
///
/// 集中在這裡是因為用到它的地方有四處（Email 註冊、社群登入的確認、既有帳號的補問、
/// 個人檔案改生日），再加上配對的年齡篩選。以前「今天是哪一天」各寫各的——
/// 有的用伺服器的當地時間、有的用 DateTime.Today——主機在 UTC 上跑，
/// 台灣的凌晨零點到八點之間，「剛滿 18 歲」在不同端點會得到不同的答案。
/// </summary>
public static class AgeRules
{
    public const int MinimumAge = 18;

    /// <summary>出生年不接受比這更早的：不是限制，是擋掉明顯填錯的（1900、0001）。</summary>
    public const int EarliestBirthYear = 1940;

    /// <summary>前後端之間傳出生年月日的格式。</summary>
    public const string BirthdayFormat = "yyyy-MM-dd";

    private static readonly TimeSpan TaiwanOffset = TimeSpan.FromHours(8);

    /// <summary>
    /// 台灣的今天。這個服務的使用者在台灣，生日是照台灣的日曆過的；
    /// 伺服器在哪個時區不該影響一個人今天滿不滿 18 歲。
    /// </summary>
    public static DateOnly Today() => DateInTaiwan(DateTimeOffset.UtcNow);

    /// <summary>某個時刻在台灣是幾月幾號。獨立出來是為了測得到——「現在」沒辦法指定，這個可以。</summary>
    public static DateOnly DateInTaiwan(DateTimeOffset instant) => DateOnly.FromDateTime(instant.ToOffset(TaiwanOffset).DateTime);

    /// <summary>讀出生年月日。格式不對、不存在的日期（2 月 30 日）、年份離譜，都回 null。</summary>
    public static DateOnly? ParseBirthday(string? input)
    {
        if (!DateOnly.TryParseExact(input?.Trim(), BirthdayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthday))
        {
            return null;
        }

        return birthday.Year >= EarliestBirthYear && birthday <= Today() ? birthday : null;
    }

    /// <summary>滿 18 歲的那一天（含）起算成年。</summary>
    public static bool IsAdult(DateOnly birthday) => birthday.AddYears(MinimumAge) <= Today();

    public static int AgeOf(DateOnly birthday)
    {
        var today = Today();
        var age = today.Year - birthday.Year;
        if (birthday > today.AddYears(-age)) age--;
        return age;
    }

    public static string Format(DateOnly birthday) => birthday.ToString(BirthdayFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// 星座。別人的卡片上顯示的是「年齡＋星座」，而不是生日本身——
    /// 所以這兩樣由後端算好再給，完整的出生年月日不必離開伺服器。
    ///
    /// 分界日必須和前端的 getZodiac 一致（frontend/src/features/profile/profileUtils.ts
    /// 與 shared/lib/userDisplay.ts 各有一份）：使用者在「我的」頁看到的自己的星座是前端算的，
    /// 別人看到的是這裡算的，兩邊對不上就是同一個人有兩個星座。
    /// 每一段是「這個星座的最後一天（月×100＋日）」，依日曆順序排。
    /// </summary>
    private static readonly (int LastDay, string Name)[] ZodiacEnds =
    [
        (119, "摩羯座"),
        (218, "水瓶座"),
        (320, "雙魚座"),
        (419, "牡羊座"),
        (520, "金牛座"),
        (620, "雙子座"),
        (722, "巨蟹座"),
        (822, "獅子座"),
        (922, "處女座"),
        (1022, "天秤座"),
        (1121, "天蠍座"),
        (1221, "射手座"),
    ];

    public static string ZodiacOf(DateOnly birthday)
    {
        var monthDay = birthday.Month * 100 + birthday.Day;
        foreach (var (lastDay, name) in ZodiacEnds)
        {
            if (monthDay <= lastDay) return name;
        }

        // 12 月 22 日之後：摩羯座跨年，年尾這一段接回年初
        return "摩羯座";
    }
}
