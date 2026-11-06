using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Controllers;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 樹洞牆上不該看得出作者是誰。
///
/// GET /api/rants 與 GET /api/rants/{id} 沒有 [Authorize]——任何人、不必登入
/// 都讀得到，而回應裡原本帶著作者的 user id。匿名發文於是可以被對應回某一個
/// 帳號，再拿那個 id 去 /api/profile/{userId} 查出這個人的生日、照片、
/// 自我介紹。整面牆的匿名性就是在這一欄上漏掉的。
///
/// 前端需要的其實只有「這篇是不是我發的」（決定要不要顯示刪除選單）。
/// </summary>
public sealed class RantAnonymityTests
{
    private const string Author = "user_author";
    private const string Reader = "user_reader";
    private const string PostId = "rant_under_test";

    [Fact]
    public void TheAuthorsUserIdNeverLeavesTheServer()
    {
        var json = JsonSerializer.Serialize(new RantPost
        {
            Id = PostId,
            UserId = Author,
            Nickname = "阿佑",
            Content = "今天很累",
            Replies = [new RantReply { Id = "reply_1", UserId = "user_replier", Content = "我懂" }],
        });

        Assert.DoesNotContain(Author, json);
        Assert.DoesNotContain("user_replier", json);
        Assert.DoesNotContain("userId", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 檢舉次數也不送。回出去等於在公開牆上寫著「這篇已經被兩個人檢舉了」，
    /// 而那是審核狀態，不是給讀者看的。
    /// </summary>
    [Fact]
    public void TheModerationCountersDoNotLeaveEither()
    {
        var json = JsonSerializer.Serialize(new RantPost { Id = PostId, ReportCount = 2, IsHidden = false });

        Assert.DoesNotContain("reportCount", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isHidden", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MyOwnPostComesBackMarkedAsMine()
    {
        var post = Payload(Controller(nameof(MyOwnPostComesBackMarkedAsMine), Author).GetOne(PostId));

        Assert.True(post.IsMine);
        Assert.True(post.Replies.Single().IsMine);
    }

    [Fact]
    public void SomebodyElsesPostIsNotMine()
    {
        var post = Payload(Controller(nameof(SomebodyElsesPostIsNotMine), Reader).GetOne(PostId));

        Assert.False(post.IsMine);
        Assert.False(post.Replies.Single().IsMine);
    }

    /// <summary>沒登入的人看整面牆，每一篇都不是他的。</summary>
    [Fact]
    public void WithoutLoggingInNothingIsMine()
    {
        var controller = Controller(nameof(WithoutLoggingInNothingIsMine), callerId: null);

        var posts = Assert.IsType<ApiResponse<List<RantPost>>>(controller.Get(null, 20).Value).Data!;

        Assert.All(posts, post => Assert.False(post.IsMine));
    }

    /// <summary>互動之後回傳的那一份，和牆上看到的要是同一份——IsMine 也一樣。</summary>
    [Fact]
    public void ThePostHandedBackAfterAnInteractionIsMarkedToo()
    {
        var controller = Controller(nameof(ThePostHandedBackAfterAnInteractionIsMarkedToo), Author);

        Assert.True(Payload(controller.Understand(PostId)).IsMine);
    }

    // ── 測試資料 ──

    private static RantPost Payload(ActionResult<ApiResponse<RantPost>> result)
    {
        var response = Assert.IsType<ApiResponse<RantPost>>(result.Value);
        Assert.NotNull(response.Data);
        return response.Data!;
    }

    private static RantsController Controller(string databaseName, string? callerId)
    {
        var db = TestWorld.Db(databaseName);
        db.Users.AddRange(TestWorld.User(Author), TestWorld.User(Reader));
        db.RantPosts.Add(new RantPostRecord { Id = PostId, UserId = Author, Nickname = "阿佑", Content = "今天很累" });
        db.RantReplies.Add(new RantReplyRecord
        {
            Id = "reply_1", RantPostId = PostId, UserId = Author, Nickname = "阿佑", Content = "補充一下",
        });
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        var rantService = new RantService(
            TestWorld.Storage(), new TodayAnalyzerService(), new ModerationService(), services.BuildServiceProvider());

        var identity = callerId == null
            ? new ClaimsIdentity()
            : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, callerId)], "test");

        return new RantsController(rantService, TestWorld.MediaUrls())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
            },
        };
    }
}
