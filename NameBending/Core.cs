using MelonLoader;
using MelonLoader.TinyJSON;
using Newtonsoft.Json;
using RumbleModUI;
using UnityEngine;
using System.IO;
using Il2CppTMPro;
using RumbleModdingAPI;
using System.Collections.Generic;
using System;
using Il2CppRUMBLE.Environment;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Managers;
using System.Linq;
using Il2CppPhoton.Pun;
using System.Collections;
using Il2CppPhoton.Realtime;
using HarmonyLib;
using static Il2CppRootMotion.FinalIK.GrounderQuadruped;

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

    public class Core : MelonMod
    {
        public static Core Instance;
        public Mod Mod = new Mod();
        public string ModFolder = "NameBending";
        private MelonPreferences_Category debugging;
        private MelonPreferences_Entry<bool> isDebugMode;

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

        const int updateCooldown = 3000;

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

            bool isLocal = player.Controller.controllerType == ControllerType.Local;
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
            Calls.onMapInitialized += sceneReady;

            debugging = MelonPreferences.CreateCategory("Debugging");
            isDebugMode = debugging.CreateEntry<bool>("DebugMode", false);

            loadFonts();
            loadShader();
            Instance = this;
            readDesignationFiles();
        }

        void sceneReady()
        {
            if (Calls.Scene.GetSceneName() == "Gym")
            {
                globalInit = true;
                ApplyComponentsToPlate(Calls.GameObjects.Gym.LOGIC.DressingRoom.PreviewPlayerController.NameTag.GetGameObject(), PlayerManager.Instance.LocalPlayer);
            }

            UpdateDesignations();
        }

        public override void OnUpdate()
        {
            if (!globalInit) return;

            if (!ModUISettings.DisableRefreshShortcut && Input.GetKeyDown(KeyCode.N))
            {
                long cooldown = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastDesignationUpdate;

                if ((cooldown > updateCooldown) || !PhotonNetwork.InRoom)
                {
                    LoggerInstance.Msg("Updating your name and title...");
                    readDesignationFiles();
                    UpdateDesignations();
                }
                else
                {
                    LoggerInstance.Error($"Wait {((double)(updateCooldown - cooldown) / 1000).ToString("F2")} seconds before updating name and title again");
                }
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
                    Font newFont = Calls.LoadAssetFromStream<Font>(this, "NameBending.assets.namebending", fontName);
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

            CachedFontAssets = fontAssets;
        }

        void loadShader()
        {
            Shader imageShader = Calls.LoadAssetFromStream<Shader>(this, "NameBending.assets.namebending", "SimpleRGBA");
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
            prop["NameBending." + type + ".HashCode"] = (Il2CppSystem.Object)variation.GetHashCode();
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

        public void OnUIInit()
        {
            Mod.ModName = BuildInfo.Name;
            Mod.ModVersion = BuildInfo.Version;
            Mod.SetFolder("NameBending");
            Mod.AddDescription("Description", "", BuildInfo.Description, new Tags { IsSummary = true });

            Mod.AddToList("<#08F>Name Plate Preview", true, 0, "Enable a preview of your bent nameplate", new Tags());
            Mod.AddToList("<#0BB>My Custom Name", true, 0, "Bend your name", new Tags());
            Mod.AddToList("<#0BB>My Custom Title", true, 0, "Bend your title", new Tags());
            Mod.AddToList("<#0BB>My Alt Name", true, 0, "Others will see the name specified under \"altText\" in the settings file when they don't have the mod\nThis might require a restart", new Tags());
            Mod.AddToList("<#B09>Others' Custom Names", true, 0, "See others' bent names", new Tags());
            Mod.AddToList("<#B09>Others' Custom Titles", true, 0, "See others' bent titles", new Tags());
            Mod.AddToList("<#B09>Animations", true, 0, "Name and title animations will be visible", new Tags());
            Mod.AddToList("<#B09>Truncation Length", -1, "Max number of characters to show on names and titles\n(set to -1 to disable)", new Tags());
            Mod.AddToList("<#888>MatchInfo Name Plates", true, 0, "Show fully customized name plates on the MatchInfo sign", new Tags());
            Mod.AddToList("<#888>Save Names to Files", true, 0, "Saves any name or title you come across to UserData/NameBending/saved_names", new Tags());
            Mod.AddToList("<#888>Disable Refresh Shortcut", false, 0, "Disables pressing N to update names and titles", new Tags());

            Mod.GetFromFile();
            Mod.ModSaved += OnUISave;

            UI.instance.AddMod(Mod);
        }

        public void OnUISave()
        {
            
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

    public static class ModUISettings
    {
        public static bool NamePlatePreview => (bool)Core.Instance.Mod.Settings[1].SavedValue;
        public static bool MyCustomName => (bool)Core.Instance.Mod.Settings[2].SavedValue;
        public static bool MyCustomTitle => (bool)Core.Instance.Mod.Settings[3].SavedValue;
        public static bool MyAltName => (bool)Core.Instance.Mod.Settings[4].SavedValue;
        public static bool OthersCustomNames => (bool)Core.Instance.Mod.Settings[5].SavedValue;
        public static bool OthersCustomTitles => (bool)Core.Instance.Mod.Settings[6].SavedValue;
        public static bool Animations => (bool)Core.Instance.Mod.Settings[7].SavedValue;
        public static int TruncationLength => (int)Core.Instance.Mod.Settings[8].SavedValue;
        public static bool MatchInfoNamePlates => (bool)Core.Instance.Mod.Settings[9].SavedValue;
        public static bool SaveNamesToFiles => (bool)Core.Instance.Mod.Settings[10].SavedValue;
        public static bool DisableRefreshShortcut => (bool)Core.Instance.Mod.Settings[11].SavedValue;
    }
}
