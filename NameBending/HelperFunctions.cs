using UnityEngine;

public static class HelperFunctions
{
    public static string ToHtmlStringRGB(this UnityEngine.Color color) // This is stripped by default ):
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
        int g = Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
        int b = Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
