namespace VSServerStats.Web.Services;

public static class Fmt
{
    public static string Playtime(double seconds)
    {
        var h = (int)(seconds / 3600);
        var m = (int)(seconds % 3600 / 60);
        return h >= 24 ? $"{h / 24}d {h % 24}h" : $"{h}h {m}m";
    }

    public static string Distance(double meters)
    {
        return meters >= 1000
            ? $"{meters / 1000:0.#} km"
            : $"{(int)meters} m";
    }
}
