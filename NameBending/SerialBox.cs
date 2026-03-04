using Il2Cpp;
using Il2CppPhoton.Pun;
using Il2CppPOpusCodec.Enums;
using Il2CppRUMBLE.Players;
using MelonLoader;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using static NameBending.Core;
using ThreeDISevenZeroR.UnityGifDecoder;
using ThreeDISevenZeroR.UnityGifDecoder.Model;
using UnityEngine.Playables;
using Il2CppSystem.Linq.Expressions;
using Microsoft.Extensions.Primitives;
using Il2CppInterop.Generator.Passes;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace NameBending
{
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

        [JsonProperty("prohibitCaching")]
        public bool ProhibitCaching = false;

        [JsonProperty("altText")]
        public string AltText = "";

        [JsonProperty("frameDuration")]
        public int FrameDuration = 0;

        [JsonProperty("loopFrames")]
        public bool LoopFrames = true;

        [JsonProperty("interpolation")]
        public bool Interpolation = false;

        [JsonProperty("enableFields")]
        public bool EnableFields = true;

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

            foreach (var field in fields)
            {
                // Boolean
                if (field.Value == "true") typedFields.Add(new TypedField(field.Key, true) { OwnerComponent = OwnerComponent});
                else if (field.Value == "false") typedFields.Add(new TypedField(field.Key, false) { OwnerComponent = OwnerComponent });

                // Numbers
                else if (float.TryParse(field.Value, out var numberValue)) typedFields.Add(new TypedField(field.Key, numberValue) { OwnerComponent = OwnerComponent });

                // Strings
                else typedFields.Add(new TypedField(field.Key, field.Value) { OwnerComponent = OwnerComponent });
            }

            _typedFields = typedFields;
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

        public int GetJsonPropertiesHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + DesignationType.GetHashCode();
                hash = hash * 23 + (OwnerID?.GetHashCode() ?? 0);
                hash = hash * 23 + (OwnerAltText?.GetHashCode() ?? 0);
                hash = hash * 23 + (Weight?.GetHashCode() ?? 0);
                hash = hash * 23 + ProhibitCaching.GetHashCode();
                hash = hash * 23 + (AltText?.GetHashCode() ?? 0);
                hash = hash * 23 + FrameDuration.GetHashCode();
                hash = hash * 23 + LoopFrames.GetHashCode();
                hash = hash * 23 + Interpolation.GetHashCode();
                hash = hash * 23 + (Frames != null ? Frames.GetHashCode() : 0);
                hash = hash * 23 + (Fonts != null ? Fonts.GetHashCode() : 0);
                hash = hash * 23 + (Images != null ? Images.GetHashCode() : 0);
                return hash;
            }
        }

        public double GetWeightFromString()
        {
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

            return 0;
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

    public class TypedField
    {
        public NameBend OwnerComponent;

        public enum FieldType
        {
            Boolean = 0,
            Number = 1,
            String = 2
        }

        public static ReadOnlyCollection<string> FactoryFields = new List<string>(){ "RANDOM", "FRAME_RANDOM", "FRAME_PROGRESS", "LOOP_PROGRESS", "FRAME", "TIMER" }.AsReadOnly();
        public bool IsFactory => FactoryFields.Contains(StringValue.Split('|')[0]);

        public string Identifier;
        public FieldType CurrentFieldType;

        public bool BooleanValue;
        public float NumberValue;
        public string StringValue;

        System.Random randy = new System.Random();
        int prevFrameIndex = 0;
        float lastOutput;

        public string GetValueAsString()
        {
            if (!IsFactory)
            {
                switch (CurrentFieldType)
                {
                    case FieldType.Boolean: return BooleanValue.ToString();
                    case FieldType.Number: return NumberValue.ToString();
                    default: return StringValue;
                }
            }
            else
            {
                var fieldParams = StringValue.Split('|');
                string param1 = fieldParams[0];
                float output = lastOutput;

                bool doRemap = false;
                bool doDilation = false;

                if (param1 == "RANDOM" || (param1 == "FRAME_RANDOM" && OwnerComponent.FrameIndex != prevFrameIndex))
                {
                    prevFrameIndex = OwnerComponent.FrameIndex;
                    output = (float)randy.NextDouble();
                    doRemap = true;
                }

                if (param1 == "FRAME_PROGRESS")
                {
                    output = OwnerComponent.FrameProgress;
                    doRemap = true;
                }

                if (param1 == "LOOP_PROGRESS")
                {
                    output = OwnerComponent.LoopProgress;
                    doRemap = true;
                }

                if (param1 == "FRAME")
                {
                    output = OwnerComponent.FrameIndex;
                    doDilation = true;
                }

                if (param1 == "TIMER")
                {
                    output = OwnerComponent.Timer;
                    doDilation = true;
                }

                // Remap to fit the second and third parameters as a min and max respectively (Field|Min|Max)
                if (doRemap)
                {
                    if (fieldParams.Length == 2)
                    {
                        if (float.TryParse(fieldParams[1], out float param2))
                        {
                            output += param2;
                        }
                    }
                    else if (fieldParams.Length > 2)
                    {
                        if (float.TryParse(fieldParams[1], out float param2))
                        {
                            if (float.TryParse(fieldParams[2], out float param3))
                            {
                                output = Mathf.Lerp(param2, param3, output);
                            }
                        }
                    }
                }

                // Remap with scale and offset (Field|Offset|Scale)
                if (doDilation)
                {
                    if (fieldParams.Length > 2)
                    {
                        if (float.TryParse(fieldParams[2], out float param3))
                        {
                            output *= param3;
                        }
                    }
                    if (fieldParams.Length > 1)
                    {
                        if (float.TryParse(fieldParams[1], out float param2))
                        {
                            output += param2;
                        }
                    }
                }

                lastOutput = output;
                return output.ToString();
            }
        }

        public TypedField(string identifier, bool booleanValue)
        {
            CurrentFieldType = FieldType.Boolean;
            Identifier = identifier;
            BooleanValue = booleanValue;
        }

        public TypedField(string identifier, float numberValue)
        {
            CurrentFieldType = FieldType.Number;
            Identifier = identifier;
            NumberValue = numberValue;
        }

        public TypedField(string identifier, string stringValue)
        {
            CurrentFieldType = FieldType.String;
            Identifier = identifier;
            StringValue = stringValue;
        }
    }

    [Serializable]
    public class ImageInfo
    {
        [JsonProperty("link")]
        public string Link;

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
                Frames.Add(new FrameData { Texture = value, HolderList = Frames });
            }
        }

        [JsonIgnore]
        public bool isGIF = false;
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

            if (!FindLinkTrust(Link) && !ForceUncensor)
            {
                Frames.Add(new FrameData
                {
                    Texture = Core.Instance.CachedCensoredTexture,
                    HolderList = Frames
                });

                TextureDownloaded = true;
                Censored = true;
                yield break;
            }
            else
            {
                Censored = false;
            }

            UnityWebRequest uwr = UnityWebRequest.Get(Link);
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                MelonLogger.Error($"Failed to download image: {Link} - {uwr.error}");
                uwr.Dispose();
                yield break;
            }

            byte[] imageBytes = uwr.downloadHandler.data;
            uwr.Dispose();

            isGIF = Link.ToLower().EndsWith(".gif");
            if (isGIF) // Add all textures to Frames list
            {
                Frames.AddRange(HelperFunctions.ConvertGifToList(imageBytes));
                foreach (FrameData frame in Frames) frame.HolderList = Frames;
            }
            else // Add image texture to first item of Frames list
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                tex.name = Link;
                if (!tex.LoadImage(imageBytes))
                {
                    MelonLogger.Error($"Failed to create texture from downloaded data: {Link}");
                    TextureDownloaded = false;
                    yield break;
                }
                Frames.Add(new FrameData
                {
                    Texture = tex,
                    HolderList = Frames
                });
            }

            TextureDownloaded = true;
        }

        public void UndownloadImage()
        {
            Frames.Clear();
            TextureDownloaded = false;
            isGIF = false;
        }

        public static bool FindLinkTrust(string link)
        {
            List<string> trustedLinks = new List<string>
            {
                "imgur.com",
                "i.imgur.com"
            };

            if (string.IsNullOrWhiteSpace(link))
                return false;

            try
            {
                var uri = new Uri(link);
                foreach (string trustedLink in trustedLinks)
                {
                    if (uri.Host.Equals(trustedLink, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch
            {
                // Invalid URL
                return false;
            }

            return false;
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
                isGIF = true,
                DoLooping = true,
                Frames = Core.Instance.CachedLoadingFrames
            };
        }
    }

    public class FrameData
    {
        public Texture2D Texture;
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
    }
}