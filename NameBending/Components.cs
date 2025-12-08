using Newtonsoft.Json;
using Il2CppTMPro;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using static NameBending.Core;
using Il2CppPhoton.Pun;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections;
using Tomlet.Exceptions;

namespace NameBending
{
    // The component to go on the text object being bent
    [RegisterTypeInIl2Cpp]
    public class NameBend : MonoBehaviour
    {
        public Variation Variation;
        public List<BentImage> BentImages = new List<BentImage>();
        public Il2CppRUMBLE.Players.Player Owner;
        private string lastKnownHash = "";
        private Il2CppPhoton.Realtime.Player photonOwner => Owner.Controller.gameObject.GetComponent<PhotonView>().Owner;

        public bool IsLocal = false;
        public DesignationType DesignationType = DesignationType.Name;
        private string typeString => DesignationType == DesignationType.Name ? "Name" : "Title";

        public string unbentText;

        public float Timer = 0f;
        private float frameDurationInSeconds => Variation.FrameDuration / 1000f;
        private float animationProgress = 0;
        private int frameIndex => (int)Math.Truncate(animationProgress);
        private float frameProgress => animationProgress - frameIndex;

        private int steps = 0; // For slower fixed update

        void Start()
        {
            Core.Instance.OnUpdateDesignations += onUpdateDesignations;
            onUpdateDesignations();
        }

        void OnDestroy()
        {
            Core.Instance.OnUpdateDesignations -= onUpdateDesignations;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.I))
            {
                foreach (BentImage bentImage in BentImages)
                {
                    object newRoutine = MelonCoroutines.Start(bentImage.SetTexture());
                    bentImage.SetTextureRoutine = newRoutine;
                }
            }
        }

        void FixedUpdate()
        {
            if (steps++ % 20 == 0) slowFixedUpdate();
            Timer += Time.fixedDeltaTime;

            if (Variation == null) return;

            Render();
        }

        void slowFixedUpdate()
        {
            if (!PhotonNetwork.InRoom) return;

            if (photonOwner.CustomProperties["NameBending." + typeString + ".HashCode"].ToString() != lastKnownHash)
            {
                FetchVariation();
            }
        }

        void onUpdateDesignations()
        {
            //if (gameObject == null) return;

            ResetAll();
            FetchVariation();
            ApplyImages();
            Timer = 0f;
        }

        public void FetchVariation()
        {
            if (IsLocal)
            {
                if (DesignationType == DesignationType.Name)
                    Variation = Core.Instance.ActiveNameVariation;
                if (DesignationType == DesignationType.Title)
                    Variation = Core.Instance.ActiveTitleVariation;
            }
            else
            {
                string fetchedVariation = photonOwner.CustomProperties["NameBending." + typeString].ToString();
                string fetchedHash = photonOwner.CustomProperties["NameBending." + typeString + ".HashCode"].ToString();
                Variation = JsonConvert.DeserializeObject<Variation>(fetchedVariation);
                lastKnownHash = fetchedHash;

                //if (ModUISettings.SaveNamesToFiles && !Variation.ProhibitCaching)
                //    cacheVariation(Variation);
            }
        }

        public void ReapplyImages()
        {
            ResetImages();
            ApplyImages();
        }

        public void ApplyImages()
        {
            if (Variation?.Images != null && Variation.Images.Count > 0)
            {
                foreach (ImageInfo imageInfo in Variation.Images)
                {
                    CreateImageObject(imageInfo, Variation);
                }
            }

            foreach (BentImage bentImage in BentImages)
            {
                object newRoutine = MelonCoroutines.Start(bentImage.SetTexture());
                bentImage.SetTextureRoutine = newRoutine;
            }
        }

        private void CreateImageObject(ImageInfo imageInfo, Variation containerVariation)
        {
            // Create a new GameObject for the plane
            GameObject planeGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            planeGO.name = "BentImagePlane";
            planeGO.transform.SetParent(this.transform, false);

            // Set position and scale from ImageInfo
            planeGO.transform.localPosition = new Vector3(imageInfo.XOffset / 100f, imageInfo.YOffset / 100f, (containerVariation.GetImageInfoIndex(imageInfo) + 1) * -0.001f);
            planeGO.transform.localScale = new Vector3(imageInfo.Width / 1000, 1f, imageInfo.Height / 1000);
            planeGO.transform.localRotation *= Quaternion.Euler(90f, 180f, 0f);

            Material mat = new Material(Core.Instance.CachedImageShader);
            mat.mainTexture = Texture2D.whiteTexture;
            planeGO.GetComponent<Renderer>().material = mat;

            // Track the image for later reset
            BentImage bentImage = new BentImage
            {
                ImageInfo = imageInfo,
                GameObject = planeGO
            };
            BentImages.Add(bentImage);
        }

        void cacheVariation(Variation variation)
        {
            string fileText = variation.SerializedJson;
            string dir = Path.Combine("UserData", Core.Instance.ModFolder, "saved_names");
            string file = Path.Combine(dir, variation.GetJsonPropertiesHashCode().ToString() + ".json");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(file, fileText);
        }

        public void Render()
        {
            if (frameDurationInSeconds > 0)
            {
                if (Variation.LoopFrames)
                    animationProgress = (Timer / frameDurationInSeconds) % Variation.FindTotalFrameCount();
                else
                    animationProgress = Math.Min(Timer / frameDurationInSeconds, Variation.FindLargestModifier());
            }
            else
            {
                animationProgress = 0;
            }

            try
            {
                int prevModifier = Variation.FindPrevModifierOfType("frame", frameIndex);
                if (Variation.Frames.ContainsKey(prevModifier))
                {
                    string frame = Variation.Frames[prevModifier];
                    if (Variation.Interpolation) frame = interpolateFrames(frame, Variation.Frames[Variation.FindNextModifierOfType("frame", frameIndex)], frameProgress);
                    SetText(frame);
                }
            }
            catch { }
            try
            {
                int prevModifier = Variation.FindPrevModifierOfType("font", frameIndex);
                if (Variation.Fonts.ContainsKey(prevModifier))
                {
                    SetFont(Variation.Fonts[prevModifier]);
                }
            }
            catch
            {
                SetFont(getFontFromName("GoodDogPlain"));
            }
        }

        public void SetText(string text)
        {
            TextMeshProUGUI textMeshProUGUI = gameObject.GetComponent<TextMeshProUGUI>();
            TextMeshPro textMeshPro = gameObject.GetComponent<TextMeshPro>();

            if (textMeshProUGUI != null)
            {
                if (String.IsNullOrEmpty(unbentText)) unbentText = textMeshProUGUI.text;
                textMeshProUGUI.text = text;
            }
            else if (textMeshPro != null)
            {
                if (String.IsNullOrEmpty(unbentText)) unbentText = textMeshPro.text;
                textMeshPro.text = text;
            }
        }

        public void SetFont(TMP_FontAsset font)
        {
            TextMeshProUGUI textMeshProUGUI = gameObject.GetComponent<TextMeshProUGUI>();
            TextMeshPro textMeshPro = gameObject.GetComponent<TextMeshPro>();

            if (textMeshProUGUI != null)
            {
                textMeshProUGUI.font = font;
            }
            else if (textMeshPro != null)
            {
                textMeshPro.font = font;
            }
        }
        public void SetFont(string fontName)
        {
            TMP_FontAsset font = getFontFromName(fontName);
            if (font != null)
            {
                SetFont(font);
            }
        }

        public void ResetText()
        {
            SetText(unbentText);
        }
        public void ResetFont()
        {
            SetFont("GoodDogPlain");
        }
        public void ResetImages()
        {
            List<BentImage> resetImages = new List<BentImage>();
            foreach (var i in BentImages)
            {
                i.Reset();
                resetImages.Add(i);
            }

            foreach (var r in resetImages) BentImages.Remove(r);
        }
        public void ResetAll()
        {
            ResetText();
            ResetFont();
            ResetImages();
        }

        string interpolateFrames(string frame1, string frame2, float progress = 0f)
        {
            //MelonLogger.Msg($"Frame 1: {frame1} | Frame 2: {frame2} | Progress: {progress}");
            //MelonLogger.Msg(progress);

            string pattern = "{(\\d+):((-?\\d*((\\.\\d*)?))|(#[A-Fa-f0-9]+))}";
            var frame1Separated = separateNumbers(frame1, pattern);
            var frame2Separated = separateNumbers(frame2, pattern);

            if (progress <= 0f) return frame1;
            if (progress >= 1f) return frame2;

            string combined = "";

            for (int i = 0; i < frame1Separated.Count; i++)
            {
                if (!frame1Separated[i].Value)
                {
                    combined += frame1Separated[i].Key;
                }
                else // If this is an interpolated value; i.e. "{1:318.4}"
                {
                    int sigFigs1 = 0;

                    string valueType1 = "number";
                    Color lerpColor1 = Color.black;
                    double lerpDouble1 = 0f;

                    var match = Regex.Match(frame1Separated[i].Key, pattern);
                    int linkIndex = int.Parse(match.Groups[1].Value);
                    string lerpValue1 = match.Groups[2].Value;
                    if (ColorUtility.TryParseHtmlString(lerpValue1, out lerpColor1))
                    {
                        valueType1 = "color";
                    }
                    if (Double.TryParse(lerpValue1, out lerpDouble1))
                    {
                        List<String> split = lerpValue1.Split(".").ToList();
                        if (split.Count > 1) sigFigs1 = Math.Clamp(split[1].Length, 1, 5);
                    }

                    string valueType2 = "number";
                    Color lerpColor2 = Color.black;
                    double lerpDouble2 = 0f;

                    var match2 = Regex.Match(frame2Separated[i].Key, pattern);
                    int linkIndex2 = int.Parse(match2.Groups[1].Value);
                    string lerpValue2 = match2.Groups[2].Value;

                    if (ColorUtility.TryParseHtmlString(lerpValue2, out lerpColor2))
                    {
                        valueType2 = "color";
                    }
                    if (Double.TryParse(lerpValue2, out lerpDouble2))
                    {
                        List<String> split = lerpValue2.Split(".").ToList();
                        int sigFigs2 = 0;
                        if (split.Count > 1) sigFigs2 = Math.Clamp(split[1].Length, 1, 5);
                        sigFigs1 = Math.Max(sigFigs1, sigFigs2);
                    }

                    if (valueType1 == "color" && valueType2 == "color")
                    {
                        //MelonLogger.Msg($"from: {HelperFunctions.ToHtmlStringRGB(lerpColor1)} | to: {HelperFunctions.ToHtmlStringRGB(lerpColor1)}");
                        combined += HelperFunctions.ToHtmlStringRGB(Color.Lerp(lerpColor1, lerpColor2, progress));
                        continue;
                    }

                    if (valueType1 == "number" && valueType2 == "number")
                    {
                        //MelonLogger.Msg($"from: {lerpDouble1} | to: {lerpDouble2}");
                        float lerped = Mathf.Lerp((float)lerpDouble1, (float)lerpDouble2, progress);
                        combined += lerped.ToString("F" + sigFigs1);
                        continue;
                    }

                    combined += frame1Separated[i];
                }
            }

            //MelonLogger.Msg(combined);
            return combined;
        }

        List<KeyValuePair<string, bool>> separateNumbers(string input, string pattern)
        {
            var parts = new List<KeyValuePair<string, bool>>();
            var matches = Regex.Matches(input, pattern);

            int lastIndex = 0;

            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    parts.Add(new (
                        input.Substring(lastIndex, match.Index - lastIndex),
                        false)); // Non-matching part
                }

                parts.Add(new (
                    match.Value,
                    true)); // Matching part
                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < input.Length)
            {
                parts.Add(new (
                    input.Substring(lastIndex),
                    false)); // Remaining non-matching part
            }

            return parts;
        }

        TMP_FontAsset getFontFromName(string font)
        {
            foreach (TMP_FontAsset f in Core.Instance.CachedFontAssets)
            {
                if (f.name == font)
                {
                    return f;
                }
            }
            return null;
        }
    }

    public class BentImage
    {
        public ImageInfo ImageInfo;
        public GameObject GameObject;
        public object SetTextureRoutine;
        public bool IsReset = false;

        public void Reset()
        {
            try
            {
                GameObject.Destroy(GameObject);
            }
            catch { }
            if (SetTextureRoutine != null)
            {
                MelonCoroutines.Stop(SetTextureRoutine);
            }
            IsReset = true;
        }

        public IEnumerator SetTexture()
        {
            int tries = 0;
            while (!ImageInfo.TextureDownloaded)
            {
                if (tries++ >= 500) yield break;
                yield return new WaitForSeconds(0.1f);
            }
            GameObject.GetComponent<Renderer>().material.mainTexture = ImageInfo.Texture;
        }
    }
}
