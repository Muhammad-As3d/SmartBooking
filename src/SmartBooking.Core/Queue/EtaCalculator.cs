namespace SmartBooking.Core.Queue;

/// <summary>
/// حساب وقت الانتظار الفعلي: مش رقم ثابت، لكن متوسط وقت كل مراجع × اللي قدامك ÷ عدد نقاط الخدمة المفتوحة.
/// </summary>
public static class EtaCalculator
{
    public static int EstimateSeconds(int aheadCount, int avgServiceSeconds, int openCounters, bool someoneInService)
    {
        if (aheadCount < 0) aheadCount = 0;
        var servers = Math.Max(1, openCounters);
        var avg = Math.Max(30, avgServiceSeconds);

        // اللي جوه حالياً متوقع يكون خلّص نص وقته في المتوسط
        var current = someoneInService ? avg / 2 : 0;
        return (int)Math.Ceiling((double)aheadCount * avg / servers) + current;
    }

    /// <summary>
    /// متوسط متحرك أُسّي: آخر المراجعين لهم وزن أكبر، وأي قيمة شاذة (مراجع اتنسى مفتوح) بتتقص.
    /// </summary>
    public static int NextAverage(int currentAvg, int observedSeconds)
    {
        var observed = Math.Clamp(observedSeconds, 30, 2 * 60 * 60);
        return (int)Math.Round(currentAvg * 0.8 + observed * 0.2);
    }

    public static string FormatMinutes(int seconds)
    {
        var minutes = (int)Math.Ceiling(seconds / 60.0);
        return minutes switch
        {
            <= 0 => "الآن",
            1 => "دقيقة",
            2 => "دقيقتين",
            <= 10 => $"{minutes} دقائق",
            _ => $"{minutes} دقيقة",
        };
    }
}
