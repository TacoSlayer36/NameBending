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
using UnityEngine.Rendering.Universal;
using Il2CppSystem.Data;
using Il2CppPhoton.Voice;
using System.Threading;

namespace NameBending
{
    // The component to go on the text object being bent
    [RegisterTypeInIl2Cpp]
    public class NameBend : MonoBehaviour
    {
        public Variation Variation;
        public List<BentImage> BentImages = new List<BentImage>();
        public Il2CppRUMBLE.Players.Player Owner;
        private Il2CppPhoton.Realtime.Player photonOwner => Owner.Controller.gameObject.GetComponent<PhotonView>().Owner;

        public bool IsLocal = false;
        public DesignationType DesignationType = DesignationType.Name;
        private string typeString => DesignationType == DesignationType.Name ? "Name" : "Title";

        public string unbentText;

        public float Timer = 0f;
        private float frameDurationInSeconds => Variation.FrameDuration / 1000f;
        public float AnimationProgress = 0;
        public int FrameIndex => (int)Math.Truncate(AnimationProgress);
        public float FrameProgress => AnimationProgress - FrameIndex;
        public float LoopProgress => AnimationProgress / (Variation.FindTotalFrameCount() * frameDurationInSeconds);

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

            FetchVariation();
        }

        void onUpdateDesignations()
        {
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
                var fetchedVariation = photonOwner.CustomProperties["NameBending." + typeString];
                string fetchedVariationString = null;
                if (fetchedVariation != null)
                {
                    fetchedVariationString = fetchedVariation.ToString();
                    Variation = JsonConvert.DeserializeObject<Variation>(fetchedVariationString);
                }

                // TODO
                //if (ModUISettings.SaveNamesToFiles && !Variation.ProhibitCaching)
                //    cacheVariation(Variation);
            }

            if (Variation != null)
                Variation.OwnerComponent = this;
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
                foreach (ImageInfo imageInfo in Variation.Images) CreateImageObject(imageInfo, Variation);
            }
            foreach (BentImage bentImage in BentImages) bentImage.RestartFrames();
        }

        private void CreateImageObject(ImageInfo imageInfo, Variation containerVariation)
        {
            // Create a new GameObject for the plane
            GameObject planeGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            planeGO.name = "BentImagePlane";
            planeGO.transform.SetParent(this.transform, false);

            // Set position and scale from ImageInfo
            float zDepth = (containerVariation.GetImageInfoIndex(imageInfo) + 1) * -0.0015f;
            if (imageInfo.ZDepth != null) zDepth = (float)imageInfo.ZDepth * -0.0015f;
            planeGO.transform.localPosition = new Vector3(imageInfo.XOffset / 100f, imageInfo.YOffset / 100f, zDepth);
            planeGO.transform.localScale = new Vector3(imageInfo.Width / 1000, 1f, imageInfo.Height / 1000);
            planeGO.transform.localRotation *= Quaternion.Euler(90f, 180f, 0f);

            Material mat = new Material(Core.Instance.CachedImageShader);
            mat.mainTexture = Core.Instance.CachedLoadingTexture;
            planeGO.GetComponent<Renderer>().material = mat;

            // Track the image for later reset
            BentImage newBentImage = planeGO.AddComponent<BentImage>();
            newBentImage.ImageInfo = imageInfo;
            BentImages.Add(newBentImage);
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
                    AnimationProgress = (Timer / frameDurationInSeconds) % Variation.FindTotalFrameCount();
                else
                    AnimationProgress = Math.Min(Timer / frameDurationInSeconds, Variation.FindLargestModifier());
            }
            else
            {
                AnimationProgress = 0;
            }

            try
            {
                int prevModifier = Variation.FindPrevModifierOfType("frame", FrameIndex);
                if (Variation.Frames.ContainsKey(prevModifier))
                {
                    string frame = Variation.Frames[prevModifier];
                    if (Variation.EnableFields) frame = processFields(frame, Variation.TypedFields);
                    //if (Variation.Interpolation) frame = interpolateFrames(frame, Variation.Frames[Variation.FindNextModifierOfType("frame", frameIndex)], frameProgress);
                    SetText(frame);
                }
            }
            catch { }
            try
            {
                int prevModifier = Variation.FindPrevModifierOfType("font", FrameIndex);
                if (prevModifier == -1) SetFont(getFontFromName("GoodDogPlain"));

                if (Variation.Fonts != null)
                {
                    if (Variation.Fonts.ContainsKey(prevModifier))
                    {
                        SetFont(Variation.Fonts[prevModifier]);
                    }
                }
            }
            catch
            { }
            try
            {
                int prevModifier = Variation.FindPrevModifierOfType("depth", FrameIndex);
                if (Variation.Depths != null && Variation.Depths.ContainsKey(prevModifier))
                {
                    string depth = Variation.Depths[prevModifier];
                    float depthFloat = 0f;
                    if (Variation.Interpolation)
                    {
                        depthFloat = Mathf.Lerp(float.Parse(depth), float.Parse(Variation.Depths[Variation.FindNextModifierOfType("depth", FrameIndex)]), FrameProgress);
                    }
                    else
                    {
                        depthFloat = float.Parse(depth);
                    }
                    SetDepth(depthFloat);
                }
            }
            catch { }
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

        public void SetDepth(float depth)
        {
            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, depth / 10f);
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

        string processFields(string inputFrame, List<TypedField> typedFields)
        {
            string pattern = "{([\\da-zA-Z_\\-]+)}";
            var matches = Regex.Matches(inputFrame, pattern);
            string outputFrame = "";

            int lastIndex = 0;
            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    outputFrame += inputFrame.Substring(lastIndex, match.Index - lastIndex);
                }

                string group1 = match.Groups[1].Value;
                var field = typedFields.FirstOrDefault(f => f.Identifier == group1);
                if (field != null)
                {
                    outputFrame += field.GetValueAsString();
                }
                else
                {
                    outputFrame += match.Value;
                }

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < inputFrame.Length)
            {
                outputFrame += inputFrame.Substring(lastIndex);
            }

            return outputFrame;
        }

        string interpolateFrames(string frame1, string frame2, float progress = 0f)
        {
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

    [RegisterTypeInIl2Cpp]
    public class BentImage : MonoBehaviour
    {
        public ImageInfo ImageInfo;
        public object SetTextureRoutine;
        public bool IsReset = false;
        float timer = 0f;
        int currentFrameIndex = 0;
        Material material => GetComponent<Renderer>().material;

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.I)) Censor();
            if (Input.GetKeyDown(KeyCode.O)) Uncensor();

            ImageInfo imageInfoToUse = ImageInfo;
            if (!imageInfoToUse.TextureDownloaded)
            {
                imageInfoToUse = ImageInfo.GetLoadingGif();
            }

            if (imageInfoToUse.isGIF)
            {
                timer += Time.deltaTime;
                if (timer > imageInfoToUse.GifDuration)
                {
                    timer = 0f;
                    currentFrameIndex = 0;
                }

                for (int i = currentFrameIndex; i < imageInfoToUse.Frames.Count; i++)
                {
                    FrameData frame = imageInfoToUse.Frames[i];

                    if (frame.GetTimestamp() < timer && frame.Index > currentFrameIndex)
                    {
                        currentFrameIndex = frame.Index;
                        material.mainTexture = imageInfoToUse.Frames[currentFrameIndex].Texture;
                        break;
                    }
                }
            }
        }

        public void Reset()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && Input.GetKeyUp(KeyCode.Escape)) Update();

            if (SetTextureRoutine != null)
            {
                MelonCoroutines.Stop(SetTextureRoutine);
            }
            IsReset = true;

            try
            {
                GameObject.Destroy(gameObject);
            }
            catch { }
        }

        public void RestartFrames()
        {
            SetTextureRoutine = MelonCoroutines.Start(SetTextureWhenAvailable(0));
            timer = 0f;
        }

        public IEnumerator SetTextureWhenAvailable(int index)
        {
            int tries = 0;
            while (!ImageInfo.TextureDownloaded)
            {
                if (tries++ >= 500) yield break;
                yield return new WaitForSeconds(0.1f);
            }
            if (!ImageInfo.isGIF) material.mainTexture = ImageInfo.Texture;
            else material.mainTexture = ImageInfo.Frames[index].Texture;
        }

        public void Censor()
        {
            ImageInfo.UndownloadImage();

            ImageInfo.Censored = true;
            ImageInfo.ForceUncensor = false;

            ImageInfo.Texture = Core.Instance.CachedCensoredTexture;
            ImageInfo.TextureDownloaded = true;

            RestartFrames();
        }

        public void Uncensor()
        {
            ImageInfo.UndownloadImage();

            ImageInfo.Censored = false;
            ImageInfo.ForceUncensor = true;

            if (ImageInfo.DownloadCoroutine != null) MelonCoroutines.Stop(ImageInfo.DownloadCoroutine);
            ImageInfo.DownloadCoroutine = MelonCoroutines.Start(ImageInfo.DownloadImage());

            RestartFrames();
        }
    }
}
