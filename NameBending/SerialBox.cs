using Il2Cpp;
using Il2CppPhoton.Pun;
using Il2CppPOpusCodec.Enums;
using Il2CppRUMBLE.Players;
using MelonLoader;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using static NameBending.Core;

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

        [JsonProperty("frames")]
        public Dictionary<int, string> Frames;

        [JsonProperty("fonts")]
        public Dictionary<int, string> Fonts;

        [JsonProperty("images")]
        public List<ImageInfo> Images;

        [JsonIgnore]
        public string SerializedJson => JsonConvert.SerializeObject(this, Formatting.None);
        [JsonIgnore]
        public string SerializedJsonIndented => JsonConvert.SerializeObject(this, Formatting.Indented);

        [JsonIgnore]
        public Player Owner; // To be set manually after deserialization

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
            Dictionary<int, string> dict = type == "font" ? Fonts : Frames;
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
            Dictionary<int, string> dict = type == "font" ? Fonts : Frames;
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

        [JsonIgnore]
        private Texture2D _texture = null;

        [JsonIgnore]
        public Texture2D Texture
        {
            get { return _texture ?? Texture2D.whiteTexture; }
        }

        [JsonIgnore]
        private float aspectRatio
        {
            get
            {
                if (_texture == null) return 0f;
                else if (_texture.height == 0 || _texture.width == 0) return 0f;
                else return _texture.height / _texture.width;
            }
        }

        [JsonIgnore]
        public float Width
        {
            get
            {
                if (_width != null) return (float)_width;
                else if (_height != null) return (float)_height * aspectRatio;
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

        public IEnumerator DownloadImage()
        {
            UnityWebRequest uwr = UnityWebRequest.Get(Link);
            uwr.downloadHandler = new DownloadHandlerBuffer();
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                MelonLogger.Error($"Failed to download image: {Link} - {uwr.error}");
                uwr.Dispose();
                yield break;
            }

            byte[] imageBytes = uwr.downloadHandler.data;
            _texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!_texture.LoadImage(imageBytes))
            {
                MelonLogger.Error($"Failed to create texture from downloaded data: {Link}");
                uwr.Dispose();
                yield break;
            }

            uwr.Dispose();
        }
    }
}