using Il2CppPhoton.Pun;
using Il2CppRUMBLE.Economy;
using Il2CppRUMBLE.Economy.Interactables;
using Il2CppRUMBLE.Players;
using MelonLoader;
using MelonLoader.ICSharpCode.SharpZipLib.Checksum;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using static NameBending.Core;

namespace NameBending;

public class Root
{
    [JsonProperty("doRandomVariations")]
    public bool DoRandomVariations = false;

    [JsonProperty("variations")]
    public List<Variation> Variations;

    public DesignationType Type; // To be set manually after deserialization
}

[Serializable]
public class Variation
{
    [JsonIgnore]
    public NameBend OwnerComponent;

    [JsonProperty("designationType")]
    public string DesignationType = "UNKNOWN"; // To be set manually after deserialization

    [JsonProperty("ownerID")]
    public string OwnerID => Owner != null ? Owner.Data.GeneralData.PlayFabMasterId : "UNKNOWN";

    [JsonProperty("ownerAltText")]
    public string OwnerAltText => Owner != null ? Owner.Data.GeneralData.PublicUsername : "UNKNOWN";

    [JsonProperty("weight")]
    public string Weight = "1";

    [JsonProperty("prohibitSaving")]
    public bool ProhibitSaving = false;

    [JsonProperty("altText")]
    public string AltText = "";

    [JsonProperty("frameDuration")]
    public int FrameDuration = 0;

    [JsonProperty("loopFrames")]
    public bool LoopFrames = true;

    [JsonProperty("interpolation")]
    public bool Interpolation = false;

    [JsonProperty("loopInterpolation")]
    public bool LoopInterpolation = true;

    [JsonProperty("enableFields")]
    public bool EnableFields = true;

    [JsonProperty("autoScaling")]
    public bool AutoScaling = true;

    [JsonProperty("fields")]
    private Dictionary<string, string> fields;

    [JsonIgnore]
    public List<TypedField> TypedFields
    {
        get
        {
            if (_typedFields == null) GenerateTypedFields();
            return _typedFields;
        }
    }
    [JsonIgnore]
    public bool FieldInstancesFound = false;

    [JsonIgnore]
    List<TypedField> _typedFields;

    [JsonProperty("frames")]
    public Dictionary<int, string> Frames;

    [JsonProperty("fonts")]
    public Dictionary<int, string> Fonts;

    [JsonProperty("depths")]
    public Dictionary<int, string> Depths;

    [JsonProperty("images")]
    public List<ImageInfo> Images;

    [JsonIgnore]
    public string SerializedJson => JsonConvert.SerializeObject(this, Formatting.None);
    [JsonIgnore]
    public string SerializedJsonIndented => JsonConvert.SerializeObject(this, Formatting.Indented);

    [JsonIgnore]
    public Player Owner; // To be set manually after deserialization

    public void GenerateTypedFields()
    {
        List<TypedField> typedFields = new List<TypedField>();
        if (fields == null)
        {
            _typedFields = typedFields;
            return;
        }

        int definitionIndex = 0;
        foreach (var field in fields)
        {
            TypedField toAdd;
            string[] identParams = field.Key.Split('|');

            // Boolean
            if (field.Value == "true") toAdd = new TypedField(true);
            else if (field.Value == "false") toAdd = new TypedField(false);

            // Numbers
            else if (float.TryParse(field.Value, out var numberValue)) toAdd = new TypedField(numberValue);

            // Colors
            else if (ColorUtility.TryParseHtmlString(field.Value, out var colorValue)) toAdd = new TypedField(colorValue);

            // Strings
            else toAdd = new TypedField(field.Value);

            toAdd.Identifier = identParams[0];
            if (identParams.Length > 1)
            {
                string param = identParams[1];
                if (param == "#")
                {
                    toAdd.CurrentFormatType = TypedField.FormatType.Hex;
                    if (identParams.Length >= 3 && int.TryParse(identParams[2], out int digits))
                        toAdd.HexDigits = digits;
                }
                else if (Regex.IsMatch(param, "\\.0*"))
                {
                    toAdd.CurrentFormatType = TypedField.FormatType.SigFigs;
                    int count = param.Count(s => s == '0');
                    toAdd.SigFigs = count;
                }
                else if (Regex.IsMatch(param, "\\d+"))
                {
                    toAdd.CurrentFormatType = TypedField.FormatType.Round;
                    if (int.TryParse(param, out int round))
                    {
                        toAdd.Round = round;
                    }
                    else
                    {
                        toAdd.CurrentFormatType = TypedField.FormatType.None;
                    }
                }
            }
            else
            {
                toAdd.CurrentFormatType = TypedField.FormatType.None;
            }

            toAdd.OwnerComponent = OwnerComponent;
            toAdd.DefinitionIndex = definitionIndex;
            definitionIndex++;

            typedFields.Add(toAdd);
        }

        _typedFields = typedFields;
        foreach (TypedField typedField in _typedFields)
            typedField.IsReferential = typedField.FindInternalRefs();
    }

    public void FindAllFieldInstances()
    {
        foreach (int frameIndex in Frames.Keys)
        {
            string frame = Frames[frameIndex];
            MatchCollection matches = Regex.Matches(frame, Core.FieldPattern);

            foreach (Match match in matches)
            {
                string group1 = match.Groups[1].Value;
                foreach (TypedField typedField in TypedFields)
                {
                    if (group1 == typedField.Identifier)
                    {
                        FieldInstance newInstance = new FieldInstance(typedField, frameIndex, match.Groups[1].Index - 1, match.Length);
                        newInstance.DefinitionIndex = ++typedField.InstanceIndexTracker;
                        if (match.Groups.Count > 2)
                        {
                            string group2 = match.Groups[2].Value;
                            if (group2.Length > 0 && group2.StartsWith('='))
                                newInstance.SetTo = group2.Substring(1);
                        }
                        typedField.Instances.Add(newInstance);
                    }
                }
            }
        }
        FieldInstancesFound = true;
    }

    public TypedField FindField(string identifier)
    {
        foreach (TypedField field in TypedFields)
        {
            if (field.Identifier == identifier) return field;
        }
        return null;
    }

    public int FindLargestModifier()
    {
        int largest = 0;
        if (Fonts?.Count > 0)
        {
            foreach (int key in Fonts.Keys)
            {
                if (key > largest) largest = key;
            }
        }
        foreach (int key in Frames.Keys)
        {
            if (key > largest) largest = key;
        }
        return largest;
    }

    public int FindSmallestModifier()
    {
        int smallest = 0;
        if (Fonts?.Count > 0)
        {
            foreach (int key in Fonts.Keys)
            {
                if (key < smallest) smallest = key;
            }
        }
        foreach (int key in Frames.Keys)
        {
            if (key < smallest) smallest = key;
        }
        return smallest;
    }

    public int FindTotalFrameCount()
    {
        return FindLargestModifier() - FindSmallestModifier() + 1;
    }

    public int FindPrevModifierOfType(string type, int index)
    {
        return FindPrevModifierOfType(type, index, LoopFrames);
    }

    public int FindNextModifierOfType(string type, int index)
    {
        return FindNextModifierOfType(type, index, LoopFrames);
    }

    public int FindPrevModifierOfType(string type, int index, bool isLoop)
    {
        Dictionary<int, string> dict;
        switch (type)
        {
            case "frame":
                dict = Frames;
                break;
            case "font":
                dict = Fonts;
                break;
            case "depth":
                dict = Depths;
                break;
            default: return -1;
        }
        if (dict == null) return -1;

        int checkCount = 0;
        int indexRange = FindTotalFrameCount();
        while (true)
        {
            if (dict.ContainsKey(index))
            {
                return index;
            }

            index--;
            if (index < FindSmallestModifier()) // Passed the beginning
            {
                if (!isLoop)
                {
                    throw new System.Exception("No previous modifier found"); // Give up
                }
                else
                {
                    index = FindLargestModifier(); // Loop if needed
                }
            }

            checkCount++;
            if (checkCount > indexRange) // Checked everything
            {
                throw new System.Exception("No previous modifier found"); // Give up
            }
        }
    }

    public int FindNextModifierOfType(string type, int index, bool isLoop)
    {
        Dictionary<int, string> dict;
        switch (type)
        {
            case "frame":
                dict = Frames;
                break;
            case "font":
                dict = Fonts;
                break;
            case "depth":
                dict = Depths;
                break;
            default: return -1;
        }
        if (dict == null) return -1;

        int checkCount = 0;
        int indexRange = FindTotalFrameCount();
        index++;
        while (!dict.ContainsKey(index))
        {
            index++;

            if (index > FindLargestModifier()) // Passed the end
            {
                if (!isLoop)
                {
                    throw new System.Exception("No next modifier found"); // Give up
                }
                else
                {
                    index = FindSmallestModifier(); // Loop if needed
                }
            }

            checkCount++;
            if (checkCount > indexRange) // Checked everything
            {
                throw new System.Exception("No next modifier found"); // Give up
            }
        }
        return index;
    }

    public void DownloadAllImages()
    {
        if (Images != null)
            foreach (ImageInfo imageInfo in Images)
            {
                imageInfo.DoLooping = LoopFrames;
                if (imageInfo.DownloadCoroutine != null) MelonCoroutines.Stop(imageInfo.DownloadCoroutine);
                imageInfo.DownloadCoroutine = MelonCoroutines.Start(imageInfo.DownloadImage());
            }
    }

    public void MarkTitleCounterfeit()
    {
        if (DesignationType != "Title") return;

        List<CatalogItem> unlockedTitles = CatalogHandler.Instance.GetCatalogItemsWithTags(CatalogItem.ItemTag.TitleItem)
                                                            .Where(i => CatalogHandler.Instance.GetUnlockStatus(i) is not GearMarket.UnlockStatus.Unlocked)
                                                            .ToList();

        foreach (KeyValuePair<int, string> frame in Frames)
        {
            string cleanedFrame = HelperFunctions.SanitizeString(HelperFunctions.RemoveInPlaceCharArray(frame.Value)).ToLower();
            if (cleanedFrame.Length >= 26) continue; // Don't need to check string similarity if it's longer than the longest base-game title

            foreach (CatalogItem title in unlockedTitles)
            {
                bool isCounterfeit = false;

                string cleanedTitle = title.Title.Split('.')[3];
                if (cleanedFrame == cleanedTitle.ToLower())
                    isCounterfeit = true;
                else if (HelperFunctions.LevenshteinDistance(cleanedFrame, cleanedTitle.ToLower()) < 3)
                    isCounterfeit = true;

                if (isCounterfeit)
                {
                    Frames[frame.Key] += "<pos=0><#F00><b>COUNTERFEIT";
                    Debug.Warning($"Counterfeit detected\nYour title: {frame.Value}\nat frame: {frame.Key}\nhas been marked as counterfeit due to being too similar to a base game title you do not own: {cleanedTitle}");
                }
            }
        }
    }

    public string GetJsonPropertiesHashCode()
    {
        string hashString = ""
        + (DesignationType ?? "")
        + (OwnerID ?? "")
        + (OwnerAltText ?? "")
        + (Weight ?? "")
        + ProhibitSaving.ToString()
        + (AltText ?? "")
        + FrameDuration.ToString()
        + LoopFrames.ToString()
        + Interpolation.ToString()
        + LoopInterpolation.ToString()
        + EnableFields.ToString()
        + AutoScaling.ToString()
        + getFramesString()
        + getFontsString()
        + getImagesString();

        byte[] inputBytes = Encoding.UTF8.GetBytes(hashString);
        byte[] hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes).Substring(0, 9);

        string getFramesString()
        {
            string combined = "";
            if (Frames == null) return "";
            foreach (var frame in Frames)
            {
                combined += frame.Key;
                combined += frame.Value ?? "";
            }
            return combined;
        }

        string getFontsString()
        {
            string combined = "";
            if (Fonts == null) return "";
            foreach (var font in Fonts)
            {
                combined += font.Key;
                combined += font.Value ?? "";
            }
            return combined;
        }

        string getImagesString()
        {
            string hash = "";
            if (Images == null) return "";
            foreach (var image in Images)
            {
                hash += image.Link ?? "";
                hash += image.Token ?? "";
                hash += image.XOffset;
                hash += image.YOffset;
                hash += image.Width;
                hash += image.Height;
                hash += image.ZDepth;
            }
            return hash;
        }
    }

    public double GetWeightFromString()
    {
        if (Weight == "") return 1;

        if (Weight.ToLower() == "host")
        {
            if (PhotonNetwork.InRoom)
                return PhotonNetwork.IsMasterClient ? 1 : 0;
            else return 1;
        }
        if (Weight.ToLower() == "client")
        {
            if (PhotonNetwork.InRoom)
                return PhotonNetwork.IsMasterClient ? 0 : 1;
            else return 0;
        }

        try
        {
            return double.Parse(Weight);
        }
        catch
        { }

        return 1;
    }

    public int GetImageInfoIndex(ImageInfo imageInfo)
    {
        for (int i = 0; i < Images.Count; i++)
        {
            if (Images[i] == imageInfo) return i;
        }

        return 0;
    }
}

[Serializable]
public class ImageInfo
{
    [JsonProperty("link")]
    public string Link;

    [JsonProperty("token")]
    public string Token;

    [JsonProperty("x")]
    public float XOffset = 0f;

    [JsonProperty("y")]
    public float YOffset = 0f;

    [JsonProperty("h")]
    private float? _height = null;

    [JsonProperty("w")]
    private float? _width = null;

    [JsonProperty("z")]
    public float? ZDepth = null;

    [JsonIgnore]
    public List<FrameData> Frames = new List<FrameData>();

    [JsonIgnore]
    public float GifDuration => Frames.Last().GetTimestamp() + Frames.Last().Delay;

    [JsonIgnore]
    public Texture2D Texture // The texture of the first frame
    {
        get
        {
            if (Frames.Count == 0) return Texture2D.whiteTexture;
            return Frames[0]?.Texture ?? Texture2D.whiteTexture;
        }
        set
        {
            Frames.Clear();
            Frames.Add(new FrameData(value, Frames, !TextureDownloaded || Censored));
        }
    }

    [JsonIgnore]
    public bool IsGIF => Frames.Count > 1;
    [JsonIgnore]
    public bool DoLooping = true;

    [JsonIgnore]
    public bool Censored = true;
    [JsonIgnore]
    public bool ForceUncensor = false;

    [JsonIgnore]
    public bool TextureDownloaded = false;

    [JsonIgnore]
    private float aspectRatio
    {
        get
        {
            if (Texture == null) return 0f;
            else if (Texture.height == 0 || Texture.width == 0) return 0f;
            else return Texture.height / Texture.width;
        }
    }

    [JsonIgnore]
    public float Width
    {
        get
        {
            if (_width != null) return (float)_width;
            else if (_height != null) return (float)_height / aspectRatio;
            else return 100f;
        }
    }

    [JsonIgnore]
    public float Height
    {
        get
        {
            if (_height != null) return (float)_height;
            else if (_width != null) return (float)_width * aspectRatio;
            else return 100f;
        }
    }

    [JsonIgnore]
    public object DownloadCoroutine;

    public IEnumerator DownloadImage()
    {
        UndownloadImage();
        if (Config.DisableMod.Value) yield break;
        if (Config.Images.Value == false) yield break;

        if (!Core.Instance.CachedImages.ContainsKey(Link))
        {
            var verifyTask = LinkVerifier.VerifyAndFetch(Link, Token, Core._http);
            while (!verifyTask.IsCompleted)
                yield return null;

            bool approved = verifyTask.IsCompletedSuccessfully && verifyTask.Result.approved;
            byte[]? imageBytes = verifyTask.IsCompletedSuccessfully ? verifyTask.Result.imageBytes : null;

            if (!approved && !ForceUncensor)
            {
                Frames.Add(new FrameData(Core.Instance.CachedCensoredTexture, Frames, true));
                TextureDownloaded = true;
                Censored = true;
                yield break;
            }
            else
            {
                Censored = false;
            }

            if (HelperFunctions.IsGif(imageBytes))
            {
                Frames.AddRange(HelperFunctions.ConvertGifToList(imageBytes!));
                foreach (FrameData frame in Frames) frame.HolderList = Frames;
            }
            else
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                tex.name = Link;
                tex.mipMapBias = Config.MipmapBias.Value;
                if (!tex.LoadImage(imageBytes!))
                {
                    Frames.Add(new FrameData(Core.Instance.CachedFailedTexture, Frames, true));
                    TextureDownloaded = true;
                    yield break;
                }
                Frames.Add(new FrameData(tex, Frames));
            }
            TextureDownloaded = true;
            if (!Core.Instance.CachedImages.ContainsKey(Link) && Frames.Count > 0)
            {
                List<FrameData> newFrames = new();
                foreach (FrameData frame in Frames) newFrames.Add(new FrameData(frame));
                Core.Instance.CachedImages[Link] = newFrames;
            }
        }
        else // Image is cached
        {
            TextureDownloaded = true;
            List<FrameData> cachedFrames = Core.Instance.CachedImages[Link];
            Frames.Clear();
            foreach (FrameData frame in cachedFrames)
            {
                FrameData newFrameData = new FrameData(frame);

                // Grab image from bytes since Texture2D is eaten on scene load
                if (newFrameData.Texture == null)
                    newFrameData.Texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                newFrameData.Texture.LoadImage(frame.CachedBytes);

                Frames.Add(newFrameData);
            }
        }
    }

    public void UndownloadImage()
    {
        Frames.Clear();
        TextureDownloaded = false;
    }

    public ImageInfo GetLoadingGif()
    {
        return new ImageInfo
        {
            Link = Link,
            XOffset = XOffset,
            YOffset = YOffset,
            _height = Height,
            _width = Width,

            TextureDownloaded = true,
            DoLooping = true,
            Frames = Core.Instance.CachedLoadingFrames
        };
    }
}

public class FrameData
{
    public Texture2D Texture;
    public byte[] CachedBytes;
    public bool Censcored = false;
    public int Index = 0;
    public float Delay = 0.001f;
    public List<FrameData> HolderList;

    public float GetTimestamp()
    {
        if (HolderList == null) return 0f;

        float timestamp = 0;
        for (int i = 0; i < Index; i++) timestamp += HolderList[i].Delay;
        return timestamp;
    }

    public FrameData(Texture2D texture, List<FrameData> holderList, bool dontCache = false)
    {
        Texture = texture;
        HolderList = holderList;

        if (!dontCache)
            CachedBytes = texture.EncodeToPNG();
    }
    public FrameData(Texture2D texture, float delay, bool dontCache = false)
    {
        Texture = texture;
        Delay = delay;

        if (!dontCache)
            CachedBytes = texture.EncodeToPNG();
    }
    public FrameData(FrameData frameData)
    {
        Texture = frameData.Texture;
        CachedBytes = frameData.CachedBytes;
        Index = frameData.Index;
        Delay = frameData.Delay;
        HolderList = frameData.HolderList;
    }
}