using Newtonsoft.Json;
using Il2CppTMPro;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static NameBending.Core;
using Il2CppPhoton.Pun;
using System.Collections;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Subsystems;
using System.Linq;

namespace NameBending
{
    // The component to go on the text object being bent
    [RegisterTypeInIl2Cpp]
    public class NameBend : MonoBehaviour
    {
        public Variation Variation;
        public List<BentImage> BentImages = new List<BentImage>();
        public Player Owner;
        public string PhotonEventString = null;
        private Il2CppPhoton.Realtime.Player photonOwner => HelperFunctions.FindPhotonPlayerFromRumblePlayer(Owner);

        public bool IsLocal = false;
        public bool IsPlayer = true;
        public bool IsScreenSpace = true;

        private PlayerNameTag _parentTag;
        public PlayerNameTag ParentTag
        {
            get
            {
                if (_parentTag != null) return _parentTag;
                _parentTag = GetComponentInParent<PlayerNameTag>();
                return _parentTag;
            }
        }

        public bool IsRemote => !IsLocal && !IsScreenSpace;
        public DesignationType DesignationType = DesignationType.Name;
        private string typeString => DesignationType == DesignationType.Name ? "Name" : "Title";

        public string unbentText = "";

        private float _timer = 0f;
        public float Timer
        {
            get
            {
                if (Config.Animations.Value)
                    return _timer;
                else
                    return 0;
            }
            set
            {
                _timer = value;
            }
        }
        private float frameDurationInSeconds => Variation.FrameDuration / 1000f;
        public float AnimationProgress = 0;
        public int FrameIndex
        {
            get
            {
                if (Config.Animations.Value)
                    return (int)Math.Truncate(AnimationProgress);
                else
                    return 0;
            }
        }
        public float FrameProgress
        {
            get
            {
                if (Config.Animations.Value)
                    return AnimationProgress - FrameIndex;
                else
                    return 0;
            }
        }
        public float LoopProgress
        {
            get
            {
                if (Config.Animations.Value)
                    return AnimationProgress / (Variation.FindTotalFrameCount() * frameDurationInSeconds);
                else
                    return 0;
            }
        }
        public int LoopCount
        {
            get
            {
                if (Config.Animations.Value)
                    return (int)(Timer / (Variation.FindTotalFrameCount() * frameDurationInSeconds));
                else
                    return 0;
            }
        }

        private int steps = 0; // For slower fixed update

        void Start()
        {
            if (!IsRemote)
            {
                Core.Instance.OnUpdateDesignations += OnUpdateDesignations;
            }
            OnUpdateDesignations();

            if (Variation != null)
            {
                TextMeshPro tmp = GetComponent<TextMeshPro>();
                if (tmp != null)
                {
                    tmp.fontStyle = FontStyles.Normal;
                    tmp.characterSpacing = 0;
                }
            }
        }

        void OnDestroy()
        {
            Core.Instance.NameBends.Remove(this);
            Core.Instance.OnUpdateDesignations -= OnUpdateDesignations;
        }

        void FixedUpdate()
        {
            //if (steps++ % 50 == 0) slowFixedUpdate();
            CheckPlayerProps();

            Timer += Time.fixedDeltaTime;

            if (Variation == null) return;

            Render();
        }

        void CheckPlayerProps()
        {
            if (!PhotonNetwork.InRoom) return;

            if (Owner?.Controller?.controllerType == Il2CppRUMBLE.Players.ControllerType.Local) return;

            if (photonOwner == null) return;

            string hash = photonOwner.CustomProperties["NameBending.HashCode"]?.ToString() ?? "";
            string storedHash = "-";

            if (Core.Instance.DesignationsHashes.ContainsKey(Owner.Controller))
                storedHash = Core.Instance.DesignationsHashes[Owner.Controller];

            if (storedHash != hash || !String.IsNullOrEmpty(PhotonEventString))
            {
                MelonCoroutines.Start(_());
                OnUpdateDesignations();
            }

            IEnumerator _()
            {
                yield return new WaitForFixedUpdate();
                Core.Instance.DesignationsHashes[Owner.Controller] = hash;
            }
        }

        public void OnUpdateDesignations()
        {
            if (this == null) return;

            ResetAll();
            if (Config.DisableMod.Value) return;

            Variation = null;
            FetchVariation();

            if (!IsRemote && DesignationType is DesignationType.Name && (!Config.MyBentName.Value || Config.DisableMod.Value)) Variation = null;
            if (IsRemote && DesignationType is DesignationType.Name && (!Config.OtherBentNames.Value || Config.DisableMod.Value)) Variation = null;
            if (!IsRemote && DesignationType is DesignationType.Title && (!Config.MyBentTitle.Value || Config.DisableMod.Value)) Variation = null;
            if (IsRemote && DesignationType is DesignationType.Title && (!Config.OtherBentTitles.Value || Config.DisableMod.Value)) Variation = null;

            if (Variation == null) return;

            ApplyImages();
            Timer = 0f;

            TextMeshPro tmp = GetComponent<TextMeshPro>();
            if (tmp != null && Variation != null)
            {
                tmp.enableAutoSizing = Variation.AutoScaling;
                tmp.fontStyle = FontStyles.Normal;
                tmp.characterSpacing = 0;
            }
        }

        public void FetchVariation()
        {
            if (Core.IsInMatch && Owner?.Controller?.ControllerType != Il2CppRUMBLE.Players.ControllerType.Local)
            {
                if (DesignationType is DesignationType.Name && Core.OpponentPhotonName != null)
                {
                    PhotonEventString = Core.OpponentPhotonName;
                    Core.OpponentPhotonName = null;
                }
                else if (DesignationType is DesignationType.Title && Core.OpponentPhotonTitle != null)
                {
                    PhotonEventString = Core.OpponentPhotonTitle;
                    Core.OpponentPhotonTitle = null;
                }
            }

            if (!String.IsNullOrEmpty(PhotonEventString))
            {
                Variation = JsonConvert.DeserializeObject<Variation>(PhotonEventString);
                PhotonEventString = null;
            }
            else if (!IsRemote)
            {
                if (DesignationType == DesignationType.Name)
                    Variation = Core.Instance.ActiveNameVariation;
                if (DesignationType == DesignationType.Title)
                    Variation = Core.Instance.ActiveTitleVariation;

                if (Variation == null)
                {
                    SetText(unbentText);
                }
            }
            else
            {
                if (photonOwner != null)
                {
                    var fetchedVariation = photonOwner.CustomProperties["NameBending." + typeString];
                    string fetchedVariationString = null;
                    if (fetchedVariation != null)
                    {
                        fetchedVariationString = fetchedVariation.ToString();
                        if (fetchedVariationString == null || fetchedVariationString == "None")
                        {
                            SetText(unbentText);
                            Variation = null;
                            return;
                        }
                        Variation = JsonConvert.DeserializeObject<Variation>(fetchedVariationString);
                    }
                }
            }

            if (Variation != null)
            {
                Variation.OwnerComponent = this;
                Variation.Owner = Owner;
                Variation.DesignationType = DesignationType.ToString();
                if (Variation.EnableFields && !Variation.FieldInstancesFound) Variation.FindAllFieldInstances();
                Variation.DownloadAllImages();
                if (Config.SaveNamesToFiles.Value && !Variation.ProhibitSaving)
                    saveVariation(Variation);
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
            if (this == null || this.transform == null) return;
            if (Config.DisableMod.Value || !Config.Images.Value) return;

            // Create a new GameObject for the plane
            GameObject planeGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            planeGO.name = "BentImagePlane";
            planeGO.transform.SetParent(transform, false);

            // Set position and scale from ImageInfo
            float zDepth = (containerVariation.GetImageInfoIndex(imageInfo) + 1) * -0.0015f;
            if (imageInfo.ZDepth != null) zDepth = (float)imageInfo.ZDepth * -0.0015f;
            planeGO.transform.localPosition = new Vector3(imageInfo.XOffset / 100f, imageInfo.YOffset / 100f, zDepth);
            planeGO.transform.localScale = new Vector3(imageInfo.Width / 1000, 1f, imageInfo.Height / 1000);
            planeGO.transform.localRotation *= Quaternion.Euler(90f, 180f, 0f);

            Material mat = new Material(Core.Instance.CachedImageShader);
            mat.mainTexture = Core.Instance.CachedLoadingTexture;
            planeGO.GetComponent<Renderer>().material = mat;

            if (IsScreenSpace) planeGO.layer = LayerMask.NameToLayer("UI");

            // Track the image for later reset
            BentImage newBentImage = planeGO.AddComponent<BentImage>();
            newBentImage.ImageInfo = imageInfo;
            newBentImage.ParentComponent = this;
            BentImages.Add(newBentImage);
            Core.Instance.BentImages.Add(newBentImage);
        }

        void saveVariation(Variation variation)
        {
            if (variation == null) return;

            string fileText = variation.SerializedJsonIndented;
            string dir = Path.Combine(UserDataPath, "saved_names");

            string hash = variation.GetJsonPropertiesHashCode();
            string ownerName = HelperFunctions.SanitizeString(variation.Owner.Data.GeneralData.PublicUsername);
            string ownerID = variation.Owner.Data.GeneralData.PlayFabMasterId;

            string file = Path.Combine(dir, $"{ownerName}, {ownerID} - {variation.DesignationType} {hash}.json");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(file, fileText);
        }

        public void Render()
        {
            bool simpleNameBend = !String.IsNullOrEmpty(Config.SimpleNameBend.Value);
            bool simpleTitleBend = !String.IsNullOrEmpty(Config.SimpleTitleBend.Value);
            if (Config.EnableSimpleConfig.Value)
            {
                if (DesignationType is DesignationType.Name && simpleNameBend)
                {
                    SetText(truncateText(Config.SimpleNameBend.Value));
                    return;
                }

                if (DesignationType is DesignationType.Title && simpleTitleBend)
                {
                    SetText(truncateText(Config.SimpleTitleBend.Value));
                    return;
                }
            }

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
                    SetText(truncateText(frame));
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
            if (String.IsNullOrEmpty(text)) return;

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
            if (this == null || this.gameObject == null) return;

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

        string truncateText(string text)
        {
            if (Config.TruncationLength.Value <= 0) return text;
            return text.Substring(0, Config.TruncationLength.Value);
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
            if (this == null || this.gameObject == null) return;

            BentImage[] bentImages = GetComponentsInChildren<BentImage>();
            if (BentImages.Count == 0 && bentImages.Length > 0)
                BentImages.AddRange(bentImages);

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

                    if (!ownerField.IsReferential && !ownerField.IsFactory)
                    {
                        if (!currentInstance.IsSetter)
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
        public NameBend ParentComponent;
        public object SetTextureRoutine;
        public bool IsReset = false;
        float timer = 0f;
        int currentFrameIndex = 0;
        Material material
        {
            get
            {
                if (this != null && gameObject != null)
                {
                    Renderer renderer = GetComponentInChildren<Renderer>();
                    if (renderer != null)
                        return renderer.material;
                }
                return null;
            }
        }

        void OnDestroy()
        {
            Core.Instance.BentImages.Remove(this);
        }

        public void Update()
        {
            if (Config.DisableMod.Value) return;
            if (Config.Images.Value == false) return;

            ImageInfo imageInfoToUse = ImageInfo;
            if (!imageInfoToUse.TextureDownloaded)
            {
                imageInfoToUse = ImageInfo.GetLoadingGif();
            }

            if (imageInfoToUse.IsGIF)
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
            while (material == null)
            {
                if (tries++ >= 500) yield break;
                yield return new WaitForSeconds(0.1f);
            }
            if (!ImageInfo.IsGIF) material.mainTexture = ImageInfo.Texture;
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
        public void CensorIfNeeded()
        {
            if (ImageInfo.Censored && !ImageInfo.ForceUncensor)
            {
                ImageInfo.UndownloadImage();

                ImageInfo.Texture = Core.Instance.CachedCensoredTexture;
                ImageInfo.TextureDownloaded = true;
                RestartFrames();
            }
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

        public void UncensorIfNeeded()
        {
            if (!ImageInfo.Censored || ImageInfo.ForceUncensor)
            {
                ImageInfo.UndownloadImage();

                ImageInfo.Censored = false;

                if (ImageInfo.DownloadCoroutine != null) MelonCoroutines.Stop(ImageInfo.DownloadCoroutine);
                ImageInfo.DownloadCoroutine = MelonCoroutines.Start(ImageInfo.DownloadImage());

                RestartFrames();
            }
        }

        public void SetMipmapBias(float bias)
        {
            foreach (FrameData frame in ImageInfo.Frames)
                frame.Texture.mipMapBias = bias;
        }

        public void SetOpacity(float opacity)
        {
            material.SetFloat("_Opacity", opacity);
        }
    }
}
