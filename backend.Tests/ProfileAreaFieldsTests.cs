using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Controllers;
using SameDaySocialApp.Domain;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// PUT /api/profile/{userId} 的所在縣市、距離偏好、偏好縣市。
///
/// 為什麼這三欄的寫入規則要釘住：它們現在會被 MatchService 拿去篩人，
/// 寫進去的值不對，後果不是一句錯誤訊息，而是「設了、但配不到任何人」——
/// 而且沒有任何地方會告訴使用者。
///
/// 另一半是互斥：距離與縣市在畫面上是擇一，但已經上架的舊版個人檔案頁只有距離輸入框，
/// 不會送偏好縣市。互斥如果只靠畫面，舊版使用者就會把兩欄同時寫出值來。
/// </summary>
public sealed class ProfileAreaFieldsTests
{
    private const string Me = "user_me";

    // ── 所在縣市 ──

    [Fact]
    public async Task LocationAreaIsSaved()
    {
        var (db, result) = await Update(nameof(LocationAreaIsSaved), Request(locationArea: "台中市"));

        Assert.IsType<ApiResponse<User>>(result.Value);
        Assert.Equal("台中市", Stored(db).LocationArea);
    }

    /// <summary>清單外的字串不收。寫得進去的話，這個人對所有設了地區偏好的人都是隱形的。</summary>
    [Theory]
    [InlineData("台北")]        // 少一個「市」
    [InlineData("臺北市")]      // 異體字，清單用的是「台」
    [InlineData("Taipei")]
    public async Task LocationAreaOutsideTheListIsRejected(string area)
    {
        var (db, result) = await Update(
            nameof(LocationAreaOutsideTheListIsRejected) + area, Request(locationArea: area));

        AssertRejected(result, "INVALID_AREA");
        Assert.Null(Stored(db).LocationArea);
    }

    [Fact]
    public async Task PreferredAreaOutsideTheListIsRejected()
    {
        var (_, result) = await Update(
            nameof(PreferredAreaOutsideTheListIsRejected), Request(preferredArea: "北部"));

        AssertRejected(result, "INVALID_AREA");
    }

    /// <summary>空字串是「清掉」，不是「清單外的值」。</summary>
    [Fact]
    public async Task EmptyStringClearsLocationArea()
    {
        var (db, _) = await Update(
            nameof(EmptyStringClearsLocationArea), Request(locationArea: ""),
            seed: user => user.LocationArea = "台中市");

        Assert.Null(Stored(db).LocationArea);
    }

    /// <summary>沒送（null）就不動——這支端點所有欄位的共同語意，舊版 App 靠它才不會把新欄位洗掉。</summary>
    [Fact]
    public async Task OmittedFieldsAreLeftAlone()
    {
        var (db, _) = await Update(
            nameof(OmittedFieldsAreLeftAlone), Request(),
            seed: user => { user.LocationArea = "台中市"; user.PreferredArea = "台北市"; });

        var stored = Stored(db);
        Assert.Equal("台中市", stored.LocationArea);
        Assert.Equal("台北市", stored.PreferredArea);
    }

    // ── 距離與縣市擇一 ──

    /// <summary>
    /// 舊版個人檔案頁的情境：只送距離、不送偏好縣市。
    /// 那個人在 onboarding 選過「依縣市」，現在改填距離——縣市要讓位。
    /// </summary>
    [Fact]
    public async Task SettingADistanceClearsThePreferredArea()
    {
        var (db, _) = await Update(
            nameof(SettingADistanceClearsThePreferredArea), Request(distanceKm: 20),
            seed: user => user.PreferredArea = "台北市");

        var stored = Stored(db);
        Assert.Equal(20, stored.DistanceKm);
        Assert.Null(stored.PreferredArea);
    }

    [Fact]
    public async Task SettingAPreferredAreaClearsTheDistance()
    {
        var (db, _) = await Update(
            nameof(SettingAPreferredAreaClearsTheDistance), Request(preferredArea: "台北市"),
            seed: user => user.DistanceKm = 20);

        var stored = Stored(db);
        Assert.Equal("台北市", stored.PreferredArea);
        Assert.Null(stored.DistanceKm);
    }

    /// <summary>
    /// 距離送 0 是清掉。null 在這支端點是「這次不改」，沒有這個約定的話
    /// 距離偏好一旦設過就永遠拿不掉。
    /// </summary>
    [Fact]
    public async Task ZeroClearsTheDistance()
    {
        var (db, _) = await Update(
            nameof(ZeroClearsTheDistance), Request(distanceKm: 0),
            seed: user => user.DistanceKm = 20);

        Assert.Null(Stored(db).DistanceKm);
    }

    /// <summary>清掉其中一個，不該順手把另一個也清掉。</summary>
    [Fact]
    public async Task ClearingOneDoesNotClearTheOther()
    {
        var (db, _) = await Update(
            nameof(ClearingOneDoesNotClearTheOther), Request(distanceKm: 0),
            seed: user => user.PreferredArea = "台北市");

        Assert.Equal("台北市", Stored(db).PreferredArea);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(501)]
    public async Task DistanceOutOfRangeIsRejected(int km)
    {
        var (db, result) = await Update(
            nameof(DistanceOutOfRangeIsRejected) + km, Request(distanceKm: km));

        AssertRejected(result, "INVALID_DISTANCE");
        Assert.Null(Stored(db).DistanceKm);
    }

    // ── 縣市表本身 ──

    /// <summary>
    /// 不釘死數字，釘相對關係：座標是縣市政府所在地的近似值，差個一兩公里無所謂，
    /// 但「台北到新北比到高雄近」這種事錯了就是整張表填錯。
    /// </summary>
    [Fact]
    public void DistancesBetweenCountiesAreSane()
    {
        Assert.Equal(0, TaiwanAreas.DistanceKm("台北市", "台北市"));
        Assert.InRange(TaiwanAreas.DistanceKm("台北市", "新北市")!.Value, 5, 20);
        Assert.InRange(TaiwanAreas.DistanceKm("台北市", "桃園市")!.Value, 20, 40);
        Assert.InRange(TaiwanAreas.DistanceKm("台北市", "台中市")!.Value, 110, 150);
        Assert.InRange(TaiwanAreas.DistanceKm("台北市", "高雄市")!.Value, 270, 320);

        // 對稱
        Assert.Equal(
            TaiwanAreas.DistanceKm("台南市", "宜蘭縣")!.Value,
            TaiwanAreas.DistanceKm("宜蘭縣", "台南市")!.Value, 6);

        Assert.Null(TaiwanAreas.DistanceKm("台北市", "北部"));
        Assert.Null(TaiwanAreas.DistanceKm(null, "台北市"));
    }

    [Fact]
    public void TheListCoversAllTwentyTwoCounties()
    {
        Assert.Equal(22, TaiwanAreas.All.Count);
        Assert.All(TaiwanAreas.All, area => Assert.True(area.EndsWith('市') || area.EndsWith('縣')));
    }

    /// <summary>
    /// 前端選單（profileFields.ts 的 AREA_OPTIONS）和這裡的清單要一字不差。
    ///
    /// 兩份是各寫各的，編譯器管不到。前端多一個，就是一個「選得到、存不進去」的選項，
    /// 使用者只會看到存檔失敗；後端多一個，則是永遠沒有人選得到的死值。
    /// 寫錯一個字（台／臺）更糟：存得進去的那一邊跟比對的那一邊對不上，
    /// 結果是安靜地配不到人。所以直接讀前端那個檔來比。
    /// </summary>
    [Fact]
    public void FrontendAreaOptionsMatchTheBackendList()
    {
        var source = File.ReadAllText(RepoFile("frontend/src/features/profile/profileFields.ts"));
        var block = Regex.Match(source, @"export const AREA_OPTIONS = \[(.*?)\]", RegexOptions.Singleline);
        Assert.True(block.Success, "profileFields.ts 裡找不到 AREA_OPTIONS——改名或搬走了的話，這條測試要跟著改");

        var frontend = Regex.Matches(block.Groups[1].Value, "'([^']+)'")
            .Select(m => m.Groups[1].Value)
            .ToList();

        var onlyFrontend = frontend.Except(TaiwanAreas.All).ToList();
        var onlyBackend = TaiwanAreas.All.Except(frontend).ToList();

        Assert.True(onlyFrontend.Count == 0 && onlyBackend.Count == 0,
            $"縣市清單對不上。只有前端有：[{string.Join(", ", onlyFrontend)}]；只有後端有：[{string.Join(", ", onlyBackend)}]");
    }

    /// <summary>
    /// 用這個測試檔自己的路徑回推 repo 根目錄，而不是從執行目錄往上找：
    /// 測試輸出常被導到別處（避免和 Visual Studio 搶 bin），從那裡往上走到不了 repo。
    /// </summary>
    private static string RepoFile(string relative, [CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)!)!, relative);

    // ── 測試資料 ──

    private static UpdateProfileRequest Request(
        string? locationArea = null, string? preferredArea = null, int? distanceKm = null)
        => new(null, null, null, null,
            DistanceKm: distanceKm, PreferredArea: preferredArea, LocationArea: locationArea);

    private static UserRecord Stored(AppDbContext db) => db.Users.Single(x => x.Id == Me);

    private static void AssertRejected(ActionResult<ApiResponse<User>> result, string code)
    {
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<User>>(bad.Value);
        Assert.Equal(code, body.Code);
    }

    private static async Task<(AppDbContext Db, ActionResult<ApiResponse<User>> Result)> Update(
        string databaseName, UpdateProfileRequest request, Action<UserRecord>? seed = null)
    {
        var db = TestWorld.Db(databaseName);
        var me = TestWorld.User(Me);
        seed?.Invoke(me);
        db.Users.Add(me);
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(db);

        var controller = new ProfileController(TestWorld.Storage(), TestWorld.MediaUrls(), services.BuildServiceProvider())
        {
            // 改別人的資料會被 Forbid 擋掉，所以呼叫者要是本人
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Me)], "test"))
                }
            }
        };

        return (db, await controller.Update(Me, request, CancellationToken.None));
    }
}
