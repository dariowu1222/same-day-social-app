using Npgsql;
using SameDaySocialApp.Infrastructure.Persistence;
using Xunit;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 連線字串沒寫的連線池設定要補上，寫了的不能動。
///
/// 線上的連線字串長這樣：有 Maximum Pool Size 與 Connection Idle Lifetime，
/// 沒有 Minimum Pool Size——於是 20 秒沒人用，連線就一條不剩，下一個請求
/// 要先花 0.3–0.4 秒重建。
/// </summary>
public sealed class ConnectionPoolDefaultsTests
{
    private const string AsDeployed =
        "Host=db.example;Port=5432;Database=postgres;Username=app;Password=secret;" +
        "Maximum Pool Size=8;Connection Idle Lifetime=20;Connection Pruning Interval=10;Timeout=15;Command Timeout=30";

    [Fact]
    public void WhatTheConnectionStringLeftOutIsFilledIn()
    {
        var tuned = new NpgsqlConnectionStringBuilder(ConnectionPoolDefaults.Apply(AsDeployed));

        Assert.Equal(ConnectionPoolDefaults.WarmConnections, tuned.MinPoolSize);
        Assert.Equal(ConnectionPoolDefaults.KeepAliveSeconds, tuned.KeepAlive);
    }

    /// <summary>6 月為了不把 pooler 的連線槽吃滿而加的那幾項，一個都不能被改掉。</summary>
    [Fact]
    public void WhatTheConnectionStringSaysIsLeftAlone()
    {
        var tuned = new NpgsqlConnectionStringBuilder(ConnectionPoolDefaults.Apply(AsDeployed));

        Assert.Equal(8, tuned.MaxPoolSize);
        Assert.Equal(20, tuned.ConnectionIdleLifetime);
        Assert.Equal(10, tuned.ConnectionPruningInterval);
        Assert.Equal(15, tuned.Timeout);
        Assert.Equal(30, tuned.CommandTimeout);
        Assert.Equal("db.example", tuned.Host);
        Assert.Equal("secret", tuned.Password);
    }

    /// <summary>要調整的人直接寫在連線字串裡就會以它為準，各種寫法都算。</summary>
    [Theory]
    [InlineData("Minimum Pool Size=5;Keepalive=90")]
    [InlineData("MinPoolSize=5;KeepAlive=90")]
    [InlineData("minimum pool size=5;KEEPALIVE=90")]
    public void AnExplicitSettingWins(string extra)
    {
        var tuned = new NpgsqlConnectionStringBuilder(ConnectionPoolDefaults.Apply($"{AsDeployed};{extra}"));

        Assert.Equal(5, tuned.MinPoolSize);
        Assert.Equal(90, tuned.KeepAlive);
    }

    /// <summary>上限設得比要留的還小時不能硬塞，Npgsql 會拒絕開連線池。</summary>
    [Fact]
    public void TheWarmCountNeverExceedsTheMaximum()
    {
        var tuned = new NpgsqlConnectionStringBuilder(
            ConnectionPoolDefaults.Apply("Host=db.example;Username=app;Password=secret;Maximum Pool Size=1"));

        Assert.Equal(1, tuned.MinPoolSize);
    }
}
