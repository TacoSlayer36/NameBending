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
using Il2CppSteamworks;
using Microsoft.VisualBasic.FileIO;
using static Il2CppSystem.Globalization.TimeSpanFormat;
using ClipperLib;

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
            Variation = null;
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
            {
                Variation.OwnerComponent = this;
                if (Variation.EnableFields) Variation.FindAllFieldInstances();
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
                int prevModifierIndex = Variation.FindPrevModifierOfType("frame", FrameIndex);
                if (Variation.Frames.ContainsKey(prevModifierIndex))
                {
                    string frame = Variation.Frames[prevModifierIndex];
                    if (Variation.EnableFields) frame = ProcessFields(prevModifierIndex, Variation);
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
            Variation = null;
            ResetText();
            ResetFont();
            ResetImages();
        }

        public static string ProcessFields(int frameIndex, Variation variation)
        {
            string inputFrame = variation.Frames[frameIndex];
            List<TypedField> typedFields = variation.TypedFields;
            string outputFrame = string.Empty;

            List<FieldInstance> fieldInstances = new();
            foreach (var field in typedFields)
                foreach (var instance in field.Instances)
                    if (instance.FrameIndex == frameIndex)
                        fieldInstances.Add(instance);

            int writePos = 0;
            if (fieldInstances.Count > 0)
            {
                for (int i = 0; i < fieldInstances.Count; i++)
                {
                    FieldInstance currentInstance = fieldInstances[i];
                    TypedField ownerField = currentInstance.OwnerField;
                    string currentValue = ownerField.GetValueAsStringAt(currentInstance, true);

                    if (writePos > inputFrame.Length) break;
                    outputFrame += inputFrame.Substring(writePos, currentInstance.StartPos - writePos);

                    if (!ownerField.IsReferential)
                    {
                        if (currentInstance.SetTo == null)
                            ownerField.SetValueUntyped(currentValue);
                        else ownerField.SetValueUntyped(currentInstance.SetTo);
                    }

                    outputFrame += currentValue;
                    writePos = currentInstance.StartPos + currentInstance.TotalLength;
                }
                if (writePos <= inputFrame.Length) outputFrame += inputFrame.Substring(writePos);
            }
            else return inputFrame;

            return outputFrame;
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
