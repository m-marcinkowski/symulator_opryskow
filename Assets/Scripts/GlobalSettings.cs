using UnityEngine;

public static class GlobalSettings
{
    private static float sprayersWidth = 6f; // Domyœlna szerokoœæ opryskiwacza
    private static float turnRadius = 4f; // Domyœlny promieñ skrêtu

    public static float SprayersWidth
    {
        get { return sprayersWidth; }
        set { sprayersWidth = Mathf.Clamp(value, 6f, 36f); }
    }

    public static float TurnRadius
    {
        get { return turnRadius; }
        set { turnRadius = Mathf.Clamp(value, 4f, 12f); }
    }
}
