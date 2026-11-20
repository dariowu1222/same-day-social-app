namespace SameDaySocialApp.Domain;

/// <summary>
/// 縣市清單，以及縣市之間大約多遠。
///
/// 為什麼是縣市而不是座標：
/// 這個 App 不向使用者要定位。所在地是他自己從清單選的縣市，所以「距離」
/// 能做到的精細度就是縣市對縣市——同一個縣市算 0，不同縣市算兩邊縣市政府所在地
/// 的直線距離。這比真的距離粗，但它是一個說得清楚的規則，
/// 而不是收了使用者的數字卻什麼都不做。
///
/// 為什麼用縣市政府所在地而不是幾何中心：
/// 人住在市區，不住在幾何中心。新北市的幾何中心在南邊山區，而人口集中在板橋
/// 一帶，用幾何中心會把「台北市對新北市」算成比實際感受遠得多。
///
/// 清單與前端的 AREA_OPTIONS（profileFields.ts）必須一致：前端拿它當選單，
/// 這裡拿它驗證寫入與比對，兩邊對不上的那個縣市會變成「選得到、但配不到任何人」。
/// </summary>
public static class TaiwanAreas
{
    /// <summary>緯度、經度（度）。縣市政府所在地，取到小數四位，縣市層級夠用。</summary>
    private static readonly Dictionary<string, (double Lat, double Lon)> Seats = new()
    {
        ["台北市"] = (25.0375, 121.5645),
        ["新北市"] = (25.0120, 121.4657),
        ["基隆市"] = (25.1283, 121.7419),
        ["桃園市"] = (24.9937, 121.3010),
        ["新竹市"] = (24.8039, 120.9647),
        ["新竹縣"] = (24.8387, 121.0178),
        ["苗栗縣"] = (24.5602, 120.8214),
        ["台中市"] = (24.1618, 120.6469),
        ["彰化縣"] = (24.0756, 120.5444),
        ["南投縣"] = (23.9099, 120.6839),
        ["雲林縣"] = (23.7092, 120.5422),
        ["嘉義市"] = (23.4801, 120.4491),
        ["嘉義縣"] = (23.4518, 120.2555),
        ["台南市"] = (22.9999, 120.2270),
        ["高雄市"] = (22.6273, 120.3014),
        ["屏東縣"] = (22.6828, 120.4879),
        ["宜蘭縣"] = (24.7302, 121.7632),
        ["花蓮縣"] = (23.9769, 121.6044),
        ["台東縣"] = (22.7554, 121.1503),
        ["澎湖縣"] = (23.5655, 119.5663),
        ["金門縣"] = (24.4369, 118.3186),
        ["連江縣"] = (26.1608, 119.9517),
    };

    public static IReadOnlyCollection<string> All => Seats.Keys;

    /// <summary>
    /// 清單裡的縣市回它自己（去掉前後空白），其餘一律 null。
    ///
    /// 認不得的值當成沒填，而不是丟例外：這個欄位以前是自由文字，
    /// 舊資料裡要是有清單外的字串，比對時應該安靜地不成立，不該讓整副牌發不出來。
    /// </summary>
    public static string? Normalize(string? area)
    {
        var trimmed = area?.Trim();
        return !string.IsNullOrEmpty(trimmed) && Seats.ContainsKey(trimmed) ? trimmed : null;
    }

    public static bool IsKnown(string? area) => Normalize(area) != null;

    /// <summary>兩個縣市之間的公里數。任一邊認不得就回 null（無從比較）。</summary>
    public static double? DistanceKm(string? from, string? to)
    {
        var a = Normalize(from);
        var b = Normalize(to);
        if (a == null || b == null) return null;
        if (a == b) return 0;

        var (lat1, lon1) = Seats[a];
        var (lat2, lon2) = Seats[b];

        // Haversine。台灣的尺度下用球面公式就夠了，誤差遠小於「縣市對縣市」本身的粗細。
        const double earthRadiusKm = 6371.0;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var h = Math.Pow(Math.Sin(dLat / 2), 2)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * earthRadiusKm * Math.Asin(Math.Sqrt(h));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
