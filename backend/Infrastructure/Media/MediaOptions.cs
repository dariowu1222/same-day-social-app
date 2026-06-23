namespace SameDaySocialApp.Infrastructure.Media;

public sealed class MediaOptions
{
    public string Provider { get; set; } = "";
    public string SupabaseUrl { get; set; } = "";
    public string ServiceKey { get; set; } = "";
    public string Bucket { get; set; } = "media";
}
