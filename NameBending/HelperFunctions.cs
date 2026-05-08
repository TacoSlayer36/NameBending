using Il2CppPhoton.Pun;
using Il2CppPhoton.Realtime;
using NameBending;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

    public static bool IsGif(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 6)
            return false;

        // Check for GIF87a or GIF89a
        return (bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F' &&
                (bytes[3] == '8') &&
                (bytes[4] == '7' || bytes[4] == '9') &&
                bytes[5] == 'a');
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
                tex.mipMapBias = Config.MipmapBias.Value;

                tex.SetPixels32(image.colors);
                tex.Apply();
                float delay = image.SafeDelaySeconds;
                if (delay < 0.001f)
                {
                    delay = 0.001f;
                }
                return new FrameData(tex, delay);

            default:
                gifStream.SkipToken(); // Other tokens
                break;
        }
        return null;
    }

    public static string SanitizeString(string Input)
    {
        string pattern = @"<[^>]*>";
        return Regex.Replace(Input, pattern, string.Empty);
    }

    public static string RemoveInPlaceCharArray(string input)
    {
        var len = input.Length;
        var src = input.ToCharArray();
        int dstIdx = 0;
        for (int i = 0; i < len; i++)
        {
            var ch = src[i];
            switch (ch)
            {
                case '\u0020':
                case '\u00A0':
                case '\u1680':
                case '\u2000':
                case '\u2001':
                case '\u2002':
                case '\u2003':
                case '\u2004':
                case '\u2005':
                case '\u2006':
                case '\u2007':
                case '\u2008':
                case '\u2009':
                case '\u200A':
                case '\u202F':
                case '\u205F':
                case '\u3000':
                case '\u2028':
                case '\u2029':
                case '\u0009':
                case '\u000A':
                case '\u000B':
                case '\u000C':
                case '\u000D':
                case '\u0085':
                    continue;
                default:
                    src[dstIdx++] = ch;
                    break;
            }
        }
        return new string(src, 0, dstIdx);
    }

    public static int LevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.IsNullOrEmpty(target) ? 0 : target.Length;
        }

        if (string.IsNullOrEmpty(target))
        {
            return source.Length;
        }

        int sourceLength = source.Length;
        int targetLength = target.Length;

        int[,] distance = new int[sourceLength + 1, targetLength + 1];

        for (int i = 0; i <= sourceLength; distance[i, 0] = i++) { }
        for (int j = 0; j <= targetLength; distance[0, j] = j++) { }

        for (int i = 1; i <= sourceLength; i++)
        {
            for (int j = 1; j <= targetLength; j++)
            {
                int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                distance[i, j] = Math.Min(
                    Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                    distance[i - 1, j - 1] + cost);
            }
        }

        return distance[sourceLength, targetLength];
    }

    public static Player FindPhotonPlayerFromRumblePlayer(Il2CppRUMBLE.Players.Player player)
    {
        if (player == null) return null;
        foreach (Player photonPlayer in PhotonNetwork.PlayerList)
        {
            if (player?.Data?.GeneralData?.actorNo == photonPlayer.ActorNumber)
            {
                return photonPlayer;
            }
        }
        return null;
    }
}