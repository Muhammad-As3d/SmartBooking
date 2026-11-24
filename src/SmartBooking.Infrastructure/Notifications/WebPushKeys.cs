using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebPush;

namespace SmartBooking.Infrastructure.Notifications;

/// <summary>
/// مفاتيح VAPID للإشعارات. الأفضل تتحط في الإعدادات (WebPush:PublicKey / PrivateKey)،
/// ولو مش موجودة بتتولد مرة واحدة وتتحفظ في App_Data عشان الاشتراكات ما تبوظش مع كل تشغيل.
/// </summary>
public sealed class WebPushKeys
{
    public string Subject { get; }
    public string PublicKey { get; }
    public string PrivateKey { get; }

    public WebPushKeys(IConfiguration config, IHostEnvironment env, ILogger<WebPushKeys> logger)
    {
        Subject = config["WebPush:Subject"] is { Length: > 0 } s ? s : "mailto:support@example.com";
        var pub = config["WebPush:PublicKey"];
        var priv = config["WebPush:PrivateKey"];

        if (string.IsNullOrWhiteSpace(pub) || string.IsNullOrWhiteSpace(priv))
        {
            var path = Path.Combine(env.ContentRootPath, "App_Data", "vapid.json");
            if (File.Exists(path))
            {
                var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(path))!;
                (pub, priv) = (stored.PublicKey, stored.PrivateKey);
            }
            else
            {
                var keys = VapidHelper.GenerateVapidKeys();
                (pub, priv) = (keys.PublicKey, keys.PrivateKey);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(new Stored(pub, priv)));
                logger.LogWarning("Generated VAPID keys at {Path}. Move them to configuration (WebPush:*) for production.", path);
            }
        }

        PublicKey = pub!;
        PrivateKey = priv!;
    }

    public VapidDetails Details => new(Subject, PublicKey, PrivateKey);

    private sealed record Stored(string PublicKey, string PrivateKey);
}
