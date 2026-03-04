using MelonLoader;
using Newtonsoft.Json;
using RumbleModUI;
using UnityEngine;
using System.IO;
using Il2CppTMPro;
using RumbleModdingAPI;
using System.Collections.Generic;
using System;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Managers;
using System.Linq;
using Il2CppPhoton.Pun;
using System.Collections;
using HarmonyLib;
using UnityEngine.UI;
using Il2CppRUMBLE.Players.Subsystems;

[assembly: MelonInfo(typeof(NameBending.Core), NameBending.BuildInfo.Name, NameBending.BuildInfo.Version, NameBending.BuildInfo.Author)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]
[assembly: MelonColor(255, 255, 248, 231)]
[assembly: MelonAuthorColor(255, 255, 248, 231)]

namespace NameBending
{
    public static class BuildInfo
    {
        public const string Name = "NameBending";
        public const string Author = "TacoSlayer36";
        public const string Version = "2.0.0";
        public const string Description = "Change your name and title to anything you like";
    }

    public partial class Core : MelonMod
    {
        public static Core Instance;
        public Mod Mod = new Mod();
        public string ModFolder = "NameBending";
        private MelonPreferences_Category debugging;
        private MelonPreferences_Entry<bool> isDebugMode;
        string sceneName => RumbleModdingAPI.RMAPI.Calls.Scene.GetSceneName();

        public GameObject ParentObject;
        public GameObject localNameplateImageObject;
        public GameObject LocalNameplateClone;
        public RawImage LocalRawNameplateImage;
        bool globalInit = false;

        public enum DesignationType
        {
            Name,
            Title
        }

        string nameConfigFileName = "CustomBentName";
        string titleConfigFileName = "CustomBentTitle";
        public bool HasSimpleConfigFile = false;
        public bool HasNameConfigFile = false;
        public bool HasTitleConfigFile = false;

        public Root NameRoot = null;
        public Root TitleRoot = null;

        public Variation ActiveNameVariation = null;
        public Variation ActiveTitleVariation = null;
        int nameVariationIndex = 0;
        int titleVariationIndex = 0; 

        // Flags for when warning messages have been shown; to avoid spamming
        bool shownNoNameFileWarning = false;
        bool shownNoTitleFileWarning = false;
        bool shownNameTemplateWarning = false;
        bool shownTitleTemplateWarning = false;
        bool shownLowFrameDurationWarning = false;

        public event Action OnUpdateDesignations;
        long lastDesignationUpdate = 0;

        public List<TMP_FontAsset> CachedFontAssets = new List<TMP_FontAsset>();
        
        public Shader CachedImageShader;
        public List<FrameData> CachedLoadingFrames;
        public Texture2D CachedCensoredTexture;
        public Texture2D CachedLoadingTexture;

        public GameObject PlatePreviewCanvas;


        [HarmonyPatch(typeof(PlayerController), "Initialize", new Type[] { typeof(Il2CppRUMBLE.Players.Player) })]
        public static class playerspawn
        {
            private static void Postfix(ref PlayerController __instance, ref Il2CppRUMBLE.Players.Player player)
            {
                Core.Instance.ApplyComponentsToPlate(player);
            }
        }

        public void ApplyComponentsToPlate(GameObject plate, Il2CppRUMBLE.Players.Player player)
        {
            NameBend nameComponent = plate.transform.GetChild(0).gameObject.AddComponent<NameBend>();
            NameBend titleComponent = plate.transform.GetChild(2).gameObject.AddComponent<NameBend>();

            bool isLocal = player.Controller.controllerType == Il2CppRUMBLE.Players.ControllerType.Local;
            nameComponent.IsLocal = isLocal;
            titleComponent.IsLocal = isLocal;
            nameComponent.DesignationType = DesignationType.Name;
            titleComponent.DesignationType = DesignationType.Title;

            nameComponent.Owner = player;
            titleComponent.Owner = player;
        }

        public void ApplyComponentsToPlate(Il2CppRUMBLE.Players.Player player)
        {
            GameObject plate = player.Controller.gameObject.transform.Find("NameTag").gameObject;
            ApplyComponentsToPlate(plate, player);
        }

        public Variation PickVariation(Root root, DesignationType designationType, bool seeded = true)
        {
            if (root == null) return null;

            Variation variation = null;

            if (root.DoRandomVariations)
            {
                System.Random randy = new System.Random();

                if (seeded)
                {
                    // Use current Unix time in decaseconds as the seed
                    long unixMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    long tenSecondSeed = unixMillis / 10000;
                    randy = new System.Random((int)(tenSecondSeed & 0xFFFFFFFF));
                }
                else
                {
                    randy = new System.Random();
                }

                double totalWeight = root.Variations.Sum(v => v.GetWeightFromString());
                double randomValue = randy.NextDouble() * totalWeight;
                double cumulativeWeight = 0;
                Variation selectedVariation = null;

                foreach (var variat in root.Variations)
                {
                    cumulativeWeight += variat.GetWeightFromString();
                    if (randomValue <= cumulativeWeight)
                    {
                        selectedVariation = variat;
                        break;
                    }
                }

                variation = selectedVariation;
            }
            else // !root.DoRandomVariations
            {
                if (designationType == DesignationType.Name) variation = root.Variations[nameVariationIndex = (nameVariationIndex + 1) % root.Variations.Count];
                if (designationType == DesignationType.Title) variation = root.Variations[titleVariationIndex = (titleVariationIndex + 1) % root.Variations.Count];
            }

            if (variation == null) return null;

            variation.DesignationType = designationType == DesignationType.Name ? "Name" : "Title";
            variation.Owner = PlayerManager.Instance.LocalPlayer;
            return variation;
        }

        public override void OnLateInitializeMelon()
        {
            UI.instance.UI_Initialized += OnUIInit;
            RumbleModdingAPI.RMAPI.Actions.onMapInitialized += sceneReady;

            debugging = MelonPreferences.CreateCategory("Debugging");
            isDebugMode = debugging.CreateEntry<bool>("DebugMode", false);

            loadFonts();
            loadShader();
            Instance = this;
            readDesignationFiles();

            hasMatchInfo = RumbleModdingAPI.RMAPI.Calls.Mods.findOwnMod("MatchInfo", "2.1.2", false);
        }

        void sceneReady(string _)
        {
            if (RumbleModdingAPI.RMAPI.Calls.Scene.GetSceneName() == "Gym")
            {
                globalInit = true;
                ParentObject = new GameObject("NameBending");
                GameObject.DontDestroyOnLoad(ParentObject);
                ApplyComponentsToPlate(RumbleModdingAPI.RMAPI.GameObjects.Gym.INTERACTABLES.DressingRoom.PreviewPlayerController.NameTag.GetGameObject(), PlayerManager.Instance.LocalPlayer);
            }

            UpdateDesignations();
        }

        public override void OnUpdate()
        {
            if (!globalInit) return;

            if (!ModUISettings.DisableRefreshShortcut && Input.GetKeyDown(KeyCode.N))
            {
                long cooldown = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastDesignationUpdate;

                LoggerInstance.Msg("Updating your name and title...");
                readDesignationFiles();
                UpdateDesignations();
            }
        }

        public void UpdateDesignations()
        {
            if (NameRoot != null) ActiveNameVariation = PickVariation(NameRoot, DesignationType.Name, PhotonNetwork.InRoom);
            if (TitleRoot != null) ActiveTitleVariation = PickVariation(TitleRoot, DesignationType.Title, PhotonNetwork.InRoom);

            if (ActiveNameVariation != null)
            {
                string altText = ActiveNameVariation.AltText;
                if (!String.IsNullOrWhiteSpace(altText) && altText.Length <= 71)
                    PlayerManager.Instance.LocalPlayer.Data.GeneralData.PublicUsername = altText;
            }

            if (PhotonNetwork.InRoom)
            {
                if (ActiveNameVariation != null) MelonCoroutines.Start(AddLocalProp("Name", ActiveNameVariation));
                if (ActiveNameVariation != null) MelonCoroutines.Start(AddLocalProp("Title", ActiveTitleVariation));
            }

            lastDesignationUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            OnUpdateDesignations?.Invoke();
        }

        void readDesignationFiles()
        {
            readDesignationFile(DesignationType.Name);
            readDesignationFile(DesignationType.Title);
        }

        void readDesignationFile(DesignationType type)
        {
            string fileName = string.Empty;
            switch (type)
            {
                case DesignationType.Name:
                    fileName = nameConfigFileName; //"CustomBentName"
                    break;
                case DesignationType.Title:
                    fileName = titleConfigFileName ; //"CustomBentTitle"
                    break;
            }

            if (File.Exists(System.IO.Path.Combine("UserData", ModFolder, "SimpleConfig.txt")))
            {
                HasSimpleConfigFile = true;
                LoggerInstance.Msg("Simple Config found; ignoring advanced options");
                return; // If SimpleConfig.txt exists, we don't need to check for the other files
            }

            string baseDir = System.IO.Path.Combine("UserData", ModFolder, fileName);
            string filePath = baseDir + ".json";
            string fileTemplatePath = baseDir + "TEMPLATE.json";
            if (File.Exists(filePath)) // Advanced config files are deserialized and stored if they exist
            {
                if (type == DesignationType.Name)
                {
                    HasNameConfigFile = true;
                    try
                    {
                        NameRoot = deserializeFile(DesignationType.Name);
                        if (NameRoot != null)
                        {
                            NameRoot.Type = DesignationType.Name;
                        }
                    }
                    catch (JsonException e)
                    {
                        LoggerInstance.Error(e.Message);
                        LoggerInstance.Error("Make sure your JSON is formatted correctly");
                    }
                    catch (FileNotFoundException)
                    {
                        LoggerInstance.Error("Bent name config file not found");
                    }
                }
                else if (type == DesignationType.Title)
                {
                    HasTitleConfigFile = true;
                    try
                    {
                        TitleRoot = deserializeFile(DesignationType.Title);
                        if (TitleRoot != null)
                        {
                            TitleRoot.Type = DesignationType.Title;
                        }
                    }
                    catch (JsonException e)
                    {
                        LoggerInstance.Error(e.Message);
                        LoggerInstance.Error("Make sure your JSON is formatted correctly");
                    }
                    catch (FileNotFoundException)
                    {
                        LoggerInstance.Error("Bent title config file not found");
                    }
                }
            }
            else // The file doesn't exist
            {
                if (type == DesignationType.Name)
                {
                    HasNameConfigFile = false;
                    if (!shownNoNameFileWarning)
                    {
                        shownNoNameFileWarning = true;
                        LoggerInstance.Msg("No bent name file found; name will not be bent");
                    }
                }
                else if (type == DesignationType.Title)
                {
                    HasTitleConfigFile = false;
                    if (!shownNoTitleFileWarning)
                    {
                        shownNoTitleFileWarning = true;
                        LoggerInstance.Msg("No bent title file found; title will not be bent");
                    }
                }
            }

            if (File.Exists(fileTemplatePath)) // Detect if the user forgot to remove TEMPLATE from the file name ;)
            {
                if (type == DesignationType.Name && !shownNameTemplateWarning)
                {
                    shownNameTemplateWarning = true;
                    LoggerInstance.Warning("TEMPLATE file detected for bent name");
                }
                else if (type == DesignationType.Title && !shownTitleTemplateWarning)
                {
                    shownTitleTemplateWarning = true;
                    LoggerInstance.Warning("TEMPLATE file detected for bent title");
                }

                if (shownNameTemplateWarning || shownTitleTemplateWarning)
                {
                    LoggerInstance.Warning("Don't forget to remove TEMPLATE from your config files");
                }
            }
        }

        Root deserializeFile(DesignationType type)
        {
            string typeString = type == DesignationType.Name ? "Name" : "Title";

            if (type == DesignationType.Name && !HasNameConfigFile)
            {
                throw new FileNotFoundException();
            }
            else if (type == DesignationType.Title && !HasTitleConfigFile)
            {
                throw new FileNotFoundException();
            }

            Root root = new Root();
            string jsonString = string.Empty;

            if (type == DesignationType.Name)
            {
                jsonString = File.ReadAllText(System.IO.Path.Combine("UserData", ModFolder, nameConfigFileName + ".json"));
            }
            else if (type == DesignationType.Title)
            {
                jsonString = File.ReadAllText(System.IO.Path.Combine("UserData", ModFolder, titleConfigFileName + ".json"));
            }

            try
            {
                root = JsonConvert.DeserializeObject<Root>(jsonString);
                foreach (Variation variation in root.Variations)
                    if (variation.Images != null)
                        foreach (ImageInfo imageInfo in variation.Images)
                        {
                            imageInfo.DoLooping = variation.LoopFrames;
                            if (imageInfo.DownloadCoroutine != null) MelonCoroutines.Stop(imageInfo.DownloadCoroutine);
                            imageInfo.DownloadCoroutine = MelonCoroutines.Start(imageInfo.DownloadImage());
                        }
            }
            catch (JsonException e)
            {
                throw new JsonException("Error deserializing bent " + typeString + " config file: " + e.Message);
            }

            if (!shownLowFrameDurationWarning)
            {
                foreach (Variation variation in root.Variations)
                {
                    if (variation.FrameDuration > 0 && variation.FrameDuration < 20)
                    {
                        LoggerInstance.Warning($"Frame duration ({variation.FrameDuration} in {typeString} config) is lower than 20ms; some frames may be skipped");
                        shownLowFrameDurationWarning = true;
                    }
                }
            }

            return root;
        }

        void loadFonts()
        {
            if (CachedFontAssets.Count > 0) return;

            List<TMP_FontAsset> fontAssets = new List<TMP_FontAsset>();
            try
            {
                foreach (string fontName in new List<string> {
                    "Arial",
                    "ChineseRocks",
                    "ComicSans",
                    "Crumble",
                    "GoodDogPlain",
                    "Impact",
                    "Minecraft",
                    "Roboto",
                    "SGA",
                    "TimesNewRoman",
                    "Tumble",
                    "TokiPona",
                    "Avasinistral",
                    "Wingdings",
                    "Papyrus",
                    "Cascadia",
                    "Hollywood",
                    "HighwayGothic"
                })
                {
                    Font newFont = RumbleModdingAPI.RMAPI.AssetBundles.LoadAssetFromStream<Font>(this, "NameBending.assets.namebending", fontName);
                    TMP_FontAsset tmpFontAsset = TMP_FontAsset.CreateFontAsset(newFont);
                    tmpFontAsset.hideFlags = HideFlags.HideAndDontSave;
                    tmpFontAsset.name = fontName;
                    fontAssets.Add(tmpFontAsset);
                }

                CachedFontAssets = fontAssets;
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Error loading fonts: {ex.Message}");
            }

            try
            {
                CachedLoadingTexture = RumbleModdingAPI.RMAPI.AssetBundles.LoadAssetFromStream<Texture2D>(this, "NameBending.assets.namebending", "LoadingTexture");
                CachedCensoredTexture = RumbleModdingAPI.RMAPI.AssetBundles.LoadAssetFromStream<Texture2D>(this, "NameBending.assets.namebending", "CensoredTexture");
                CachedLoadingFrames = HelperFunctions.ConvertGifToList(RumbleModdingAPI.RMAPI.AssetBundles.LoadAssetFromStream<TextAsset>(this, "NameBending.assets.namebending", "LoadingGif").bytes);

                CachedLoadingTexture.hideFlags = HideFlags.HideAndDontSave;
                CachedCensoredTexture.hideFlags = HideFlags.HideAndDontSave;
                foreach (FrameData frame in CachedLoadingFrames)
                {
                    frame.Texture.hideFlags = HideFlags.HideAndDontSave;
                    frame.HolderList = CachedLoadingFrames;
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Error loading built-in textures: {ex.Message}");
            }

            CachedFontAssets = fontAssets;
        }

        void loadShader()
        {
            Shader imageShader = RumbleModdingAPI.RMAPI.AssetBundles.LoadAssetFromStream<Shader>(this, "NameBending.assets.namebending", "SimpleRGBA");
            imageShader.hideFlags = HideFlags.HideAndDontSave;
            CachedImageShader = imageShader;
        }

        public IEnumerator AddLocalProp(string type, Variation variation)
        {
            if (!PhotonNetwork.InRoom)
            {
                yield break;
            }
            Il2CppPhoton.Realtime.Player local = null;

            int tries = 0;
            while (local == null)
            {
                if (tries > 300)
                {
                    LoggerInstance.Error("Failed to get local Photon player");
                    yield break;
                }

                try
                {
                    local = PlayerManager.instance.localPlayer.Controller.gameObject.GetComponent<PhotonView>().Owner;
                }
                catch { }
                if (local == null)
                {
                    yield return new WaitForSeconds(0.2f);
                    tries++;
                }
            }
            Il2CppExitGames.Client.Photon.Hashtable prop = new();
            prop["NameBending." + type] = (Il2CppSystem.Object)variation.SerializedJson;
            local.SetCustomProperties(prop);
        }

        public List<string> GetCachedHashes()
        {
            string directoryPath = Path.Combine("UserData", ModFolder, "cache");
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            List<string> cachedConfigHashes = new List<string>();

            if (Directory.GetFiles(directoryPath).Length == 0)
                return cachedConfigHashes;

            foreach (string filePath in Directory.GetFiles(directoryPath))
            {
                cachedConfigHashes.Add(Path.GetFileNameWithoutExtension(filePath));
            }

            return cachedConfigHashes;
        }

        public void CreatePlatePreview()
        {
            GameObject localNameplateCamera = new GameObject("Local Nameplate Camera");
            localNameplateCamera.transform.SetParent(ParentObject.transform);
            Camera meshCamera = localNameplateCamera.AddComponent<Camera>();
            meshCamera.clearFlags = CameraClearFlags.SolidColor;
            meshCamera.backgroundColor = new Color(0, 0, 0, 0);

            RenderTexture renderTexture = new RenderTexture(512, 512, 16);
            renderTexture.Create();
            meshCamera.targetTexture = renderTexture;
            meshCamera.orthographic = false;
            meshCamera.transform.position = new Vector3(0, -1000, 1);
            meshCamera.transform.rotation = Quaternion.Euler(0, 180, 0);

            GameObject nameplate = PlayerManager.Instance.LocalPlayer.Controller.transform.Find("NameTag").gameObject;
            LocalNameplateClone = GameObject.Instantiate(nameplate);
            LocalNameplateClone.transform.SetParent(ParentObject.transform);
            LocalNameplateClone.active = true;
            LocalNameplateClone.transform.position = new Vector3(0, -1000, 0);

            LocalRawNameplateImage.texture = renderTexture;

            RectTransform rectTransform = localNameplateImageObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(300, 300);
            rectTransform.anchoredPosition = new Vector2(180, -90);
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            LocalNameplateClone.active = true;

            PlayerNameTag nameTag = LocalNameplateClone.GetComponent<PlayerNameTag>();
            nameTag.followTarget = null;
            nameTag.parentController = PlayerManager.Instance.LocalPlayer.Controller;
            nameTag.RefreshNameTag();
        }

        public void Log(string message, bool debugOnly = false, int logLevel = 0)
        {
            if (debugOnly && !isDebugMode.Value)
                return;

            switch (logLevel)
            {
                case 1:
                    LoggerInstance.Warning(message);
                    break;
                case 2:
                    LoggerInstance.Error(message);
                    break;
                default:
                    LoggerInstance.Msg(message);
                    break;
            }
        }
    }
}
