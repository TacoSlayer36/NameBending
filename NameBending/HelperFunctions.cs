using NameBending;
using System.Collections.Generic;
using ThreeDISevenZeroR.UnityGifDecoder;
using UnityEngine;

public static class HelperFunctions
{
    public static string ToHtmlStringRGB(this UnityEngine.Color color) // This is stripped by default :(
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
        int g = Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
        int b = Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    public static List<FrameData> ConvertGifToList(byte[] bytes)
    {
        List<FrameData> frameList = new List<FrameData>();

        var gifStream = new GifStream(bytes);
        int index = 0;
        while (gifStream.HasMoreData)
        {
            FrameData frame = ReadNextGifFrame(gifStream);
            if (frame != null && frame.Texture != null)
            {
                frame.Index = index++;
                frameList.Add(frame);
            }
        }

        return frameList;
    }

    private static FrameData ReadNextGifFrame(GifStream gifStream)
    {
        switch (gifStream.CurrentToken)
        {
            case GifStream.Token.Image:
                var image = gifStream.ReadImage();
                var tex = new Texture2D(
                    gifStream.Header.width,
                    gifStream.Header.height,
                    TextureFormat.ARGB32, false);

                tex.SetPixels32(image.colors);
                tex.Apply();
                float delay = image.SafeDelaySeconds;
                if (delay < 0.001f)
                {
                    delay = 0.001f;
                }

                // We have to store the texture and the delay for the playback,
                // because the delay can be irregular, and we save memory by preallocating everything.
                return new FrameData
                {
                    Texture = tex,
                    Delay = delay
                };

            default:
                gifStream.SkipToken(); // Other tokens
                break;
        }
        return null;
    }
}
