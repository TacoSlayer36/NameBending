using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;
using System.IO;
using Il2CppTMPro;
using System.Collections.Generic;
using System;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Managers;
using System.Linq;
using Il2CppPhoton.Pun;
using System.Collections;
using HarmonyLib;
using UnityEngine.UI;
using System.Net.Http;
using Il2CppRUMBLE.Players.Subsystems;
using System.Text;
using System.Security.Cryptography;
using Il2CppExitGames.Client.Photon;
using Il2CppRUMBLE.Interactions.InteractionBase;

[assembly: MelonInfo(typeof(NameBending.Core), NameBending.BuildInfo.Name, NameBending.BuildInfo.Version, NameBending.BuildInfo.Author)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]
[assembly: MelonColor(255, 255, 248, 231)]
[assembly: MelonAuthorColor(255, 255, 248, 231)]
[assembly: MelonAdditionalDependencies("UIFramework")]

namespace NameBending;

public static class BuildInfo
{
    public const string Name = "NameBending";
    public const string Author = "TacoSlayer36";
    public const string Version = "2.0.0";
    public const string Description = "Change your name and title to anything you like";
}

/* TODO:
 * More variation weight fields
 * More regular fields
 * Fix nested field bugs
 * Fix interpolation with nested fields
 *
 * 
 * TO TEST:
 * Save names to file feature
 * Changing names in matches
 * Variation weighting with fields
*/

public partial class Core : MelonMod
{
    public static Core Instance;

    bool globalInit = false;
    public static string CurrentScene = "Loader";
    public static bool IsInMatch => Core.CurrentScene.StartsWith("Map") && PhotonNetwork.PlayerList.Count == 2;

    public const byte EventNumber = 31;
    public bool EventRaised = false;
    public static string OpponentPhotonName = null;
    public static string OpponentPhotonTitle = null;

    public GameObject ModParent;
    public GameObject localNameplateImageObject;
    public GameObject LocalNameplateClone;
    public RawImage LocalRawNameplateImage;
    public GameObject CanvasObject;

    public static readonly HttpClient _http = new();
    public static string UserDataPath => Path.Combine("UserData", BuildInfo.Name);
    public const int AltTextCharLimit = 71;

    public enum DesignationType
    {
        Name,
        Title
    }

    string nameConfigFileName = "CustomBentName";
    string titleConfigFileName = "CustomBentTitle";
    public bool HasNameConfigFile = false;
    public bool HasTitleConfigFile = false;

    public const int FloatingPointPrecision = 5;
    public const string FieldPattern = "{([\\da-zA-Z_\\-|.#]+)(=[^}]+)?}";

    public Root NameRoot = null;
    public Root TitleRoot = null;

    public Variation ActiveNameVariation = null;
    public Variation ActiveTitleVariation = null;
    int nameVariationIndex = 0;
    int titleVariationIndex = 0;

    public string MyDesignationsHash
    {
        get
        {
            string hashString = (ActiveNameVariation?.GetJsonPropertiesHashCode() + ActiveTitleVariation?.GetJsonPropertiesHashCode()) ?? "";

            byte[] inputBytes = Encoding.UTF8.GetBytes(hashString);
            byte[] hashBytes = SHA256.HashData(inputBytes);
            return Convert.ToHexString(hashBytes).Substring(0, 9);
        }
    }
    public Dictionary<PlayerController, string> DesignationsHashes = new();

    public List<NameBend> NameBends = new();
    public List<BentImage> BentImages = new();

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
    public Texture2D CachedFailedTexture;

    public Dictionary<string, List<FrameData>> CachedImages = new();

    public GameObject PlatePreviewCanvas;


    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.Initialize), new Type[] { typeof(Il2CppRUMBLE.Players.Player) })]
    public static class playerspawn
    {
        private static void Postfix(ref PlayerController __instance, ref Il2CppRUMBLE.Players.Player player)
        {
            bool isLocal = player.Controller.controllerType == Il2CppRUMBLE.Players.ControllerType.Local;
            Core.Instance.ApplyComponentsToPlate(player, true, isLocal, false);
        }
    }

    [HarmonyPatch(typeof(PlayerNameTag), nameof(PlayerNameTag.ChangeOpacity), new Type[] { typeof(float) })]
    public static class nametagopacity
    {
        private static void Postfix(ref PlayerNameTag __instance, ref float alpha)
        {
            foreach (NameBend nameBend in __instance.GetComponentsInChildren<NameBend>())
                if (nameBend != null && !nameBend.IsScreenSpace)
                {
                    foreach (BentImage image in nameBend.BentImages)
                        image.SetOpacity(alpha);
                }
        }
    }

    public IEnumerator ApplyComponentsToPlate(GameObject plate, Il2CppRUMBLE.Players.Player player, bool isPlayer, bool isLocal, bool isUI)
    {
        PlayerNameTag nameTag = plate.GetComponent<PlayerNameTag>();
        nameTag.RefreshNameTag();

        foreach (TextMeshPro tmp in plate.GetComponentsInChildren<TextMeshPro>())
        {
            tmp.fontStyle = FontStyles.Normal;
            tmp.characterSpacing = 0;
        }

        MelonCoroutines.Start(applyWhenAble(DesignationType.Name));
        MelonCoroutines.Start(applyWhenAble(DesignationType.Title));
        yield break;

        IEnumerator applyWhenAble(DesignationType type)
        {
            if (!isLocal)
            {
                Il2CppPhoton.Realtime.Player photonOwner = null;

                int tries = 0;
                while (tries++ < 20)
                {
                    if (photonOwner?.CustomProperties != null) break;
                    if (type is DesignationType.Name && !String.IsNullOrEmpty(Core.OpponentPhotonName)) break;
                    if (type is DesignationType.Title && !String.IsNullOrEmpty(Core.OpponentPhotonTitle)) break;

                    foreach (var photonPlayer in PhotonNetwork.PlayerList)
                    {
                        if (player?.Data?.GeneralData?.actorNo == photonPlayer.ActorNumber)
                        {
                            photonOwner = photonPlayer;
                            break;
                        }
                    }
                    yield return new WaitForSeconds(0.25f);
                }
            }

            int childIndex = type is DesignationType.Name ? 0 : 2;
            NameBend component = plate.transform.GetChild(childIndex).gameObject.AddComponent<NameBend>();
            component.IsLocal = isLocal;
            component.IsPlayer = isPlayer;
            component.IsScreenSpace = isUI;
            component.DesignationType = type;
            component.Owner = player;
            component.ApplyImages();
            Core.Instance.NameBends.Add(component);
        }
    }

    public void ApplyComponentsToPlate(Il2CppRUMBLE.Players.Player player, bool isPlayer, bool isLocal, bool isUI)
    {
        GameObject plate = player.Controller.PlayerNameTag.gameObject;
        MelonCoroutines.Start(ApplyComponentsToPlate(plate, player, isPlayer, isLocal, isUI));
    }

    public Variation PickVariation(Root root, DesignationType designationType)
    {
        if (root == null) return null;

        Variation variation = null;

        if (root.DoRandomVariations)
        {
            System.Random randy = new System.Random();

            double totalWeight = root.Variations.Sum(v => v.GetWeightFromString());
            double randomValue = randy.NextDouble() * totalWeight;
            double cumulativeWeight = 0;
            Variation selectedVariation = null;

            foreach (var v in root.Variations)
            {
                cumulativeWeight += v.GetWeightFromString();
                if (randomValue <= cumulativeWeight)
                {
                    selectedVariation = v;
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

        if (Config.ForceVariationAt.Value >= 0 && Config.ForceVariationAt.Value < root.Variations.Count)
            variation = root.Variations[Config.ForceVariationAt.Value];

        if (variation == null) return null;

        variation.DesignationType = designationType == DesignationType.Name ? "Name" : "Title";
        variation.Owner = PlayerManager.Instance.LocalPlayer;
        return variation;
    }

    public override void OnLateInitializeMelon()
    {
        Instance = this;
        //PhotonNetwork.NetworkingClient.EventReceived += (Action<EventData>)EventReceived;

        Config.SetUpUI();
        loadFonts();
        loadShader();
        readDesignationFiles();
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        EventRaised = false;
        OpponentPhotonName = null;
        OpponentPhotonTitle = null;
        GameObject.Destroy(MatchInfoBoard.PlateClone1);
        GameObject.Destroy(MatchInfoBoard.PlateClone2);

        MelonCoroutines.Start(listenForLandButton("FlatLand"));
        MelonCoroutines.Start(listenForLandButton("VoidLand"));
        IEnumerator listenForLandButton(string landType)
        {
            yield return new WaitForSeconds(3f);
            GameObject.Find(landType)?.
                GetComponentInChildren<InteractionButton>().
                onPressed.
                AddListener(new System.Action(() =>
                {
                    MelonCoroutines.Start(OnLandEntered());
                }));
            yield break;
        }
    }
    private IEnumerator OnLandEntered()
    {
        yield return new WaitForSeconds(1.5f);
        ModParent?.SetActive(true);
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        CurrentScene = sceneName;

        if (sceneName == "Gym")
        {
            if (!globalInit)
            {
                globalInit = true;
                Config.OnPrefsSaved();
                MelonCoroutines.Start(MatchInfoBoard.FindMatchInfoBoard());
            }

            GameObject mannequinPlate = RumbleModdingAPI.RMAPI.GameObjects.Gym.INTERACTABLES.DressingRoom.PreviewPlayerController.NameTag.GetGameObject();
            MelonCoroutines.Start(ApplyComponentsToPlate(mannequinPlate, PlayerManager.Instance.LocalPlayer, false, true, false));
        }

        if (buildIndex >= 3) // Match scenes
        {
            MelonCoroutines.Start(MatchInfoBoard.SetUpMatchInfoDelayed(1f));
        }

        if (!globalInit) return;

        MelonCoroutines.Start(_());
        IEnumerator _()
        {
            yield return new WaitForSeconds(0.5f);
            readDesignationFiles();
            UpdateDesignations();
        }
    }

    public override void OnUpdate()
    {
        if (!globalInit) return;

        if (Input.GetKeyDown(Config.RefreshHotkeyCode))
        {
            readDesignationFiles();
            UpdateDesignations();
        }
    }
    public void UpdateDesignations()
    {
        long cooldown = 3000 - (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastDesignationUpdate);
        if (cooldown > 0 && PhotonNetwork.InRoom)
        {
            Debug.Error($"Please wait {Mathf.Round(cooldown / 1000).ToString("0")} seconds before updating your name again");
            return;
        }

        if (globalInit)
        {
            string message = "Updating your name and title...";
            if (Core.Instance.HasNameConfigFile && !Core.Instance.HasTitleConfigFile) message = "Updating your name...";
            if (!Core.Instance.HasNameConfigFile && Core.Instance.HasTitleConfigFile) message = "Updating your title...";
            if (!Core.Instance.HasNameConfigFile && !Core.Instance.HasTitleConfigFile) message = "Nothing to update";
            Debug.Log(message);
        }

        if (NameRoot != null && Config.MyBentName.Value && !Config.DisableMod.Value)
            ActiveNameVariation = PickVariation(NameRoot, DesignationType.Name);
        else ActiveNameVariation = null;

        if (TitleRoot != null && Config.MyBentTitle.Value && !Config.DisableMod.Value)
            ActiveTitleVariation = PickVariation(TitleRoot, DesignationType.Title);
        else ActiveTitleVariation = null;

        if (ActiveTitleVariation != null) ActiveTitleVariation.MarkTitleCounterfeit();

        string altText = PlayerManager.Instance.LocalPlayer.Data.GeneralData.PublicUsername;
        if (Config.EnableSimpleConfig.Value && !String.IsNullOrEmpty(Config.SimpleAltText.Value))
        {
            altText = Config.SimpleAltText.Value;
        }
        else if (ActiveNameVariation != null)
        {
            altText = ActiveNameVariation.AltText;
        }

        if (Config.MyAltName.Value && !String.IsNullOrWhiteSpace(altText) && altText.Length <= Core.AltTextCharLimit)
            PlayerManager.Instance.LocalPlayer.Data.GeneralData.PublicUsername = altText;

        if (altText.Length > Core.AltTextCharLimit)
            Debug.Error($"Alt text cannot be more than {Core.AltTextCharLimit} characters");

        if (PhotonNetwork.InRoom)
        {
            MelonCoroutines.Start(AddLocalProp("Name", GetVariationString(ActiveNameVariation)));
            MelonCoroutines.Start(AddLocalProp("Title", GetVariationString(ActiveTitleVariation)));
            MelonCoroutines.Start(AddLocalProp("HashCode", MyDesignationsHash));
        }

        lastDesignationUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        OnUpdateDesignations?.Invoke();


    }

    public string GetVariationString(Variation variation)
    {
        if (variation == null)
            return "None";

        if (variation.DesignationType == "Name")

        {
            if (Config.EnableSimpleConfig.Value && !String.IsNullOrEmpty(Config.SimpleNameBend.Value))
                return "{\"frames\":{\"0\":\"" + Config.SimpleNameBend.Value + "\"}}";
        }

        if (variation.DesignationType == "Title")
        {
            if (Config.EnableSimpleConfig.Value && !String.IsNullOrEmpty(Config.SimpleTitleBend.Value))
                return "{\"frames\":{\"0\":\"" + Config.SimpleTitleBend.Value + "\"}}";
        }

        return variation.SerializedJson;
    }

    public string GetVariationHash(Variation variation)
    {
        if (variation == null)
            return "";

        if (variation.DesignationType == "Name")
        {
            if (Config.EnableSimpleConfig.Value && !String.IsNullOrEmpty(Config.SimpleNameBend.Value))
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(Config.SimpleNameBend.Value);
                byte[] hashBytes = SHA256.HashData(inputBytes);
                return Convert.ToHexString(hashBytes).Substring(0, 9);
            }
        }

        if (variation.DesignationType == "Title")
        {
            if (Config.EnableSimpleConfig.Value && !String.IsNullOrEmpty(Config.SimpleTitleBend.Value))
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(Config.SimpleTitleBend.Value);
                byte[] hashBytes = SHA256.HashData(inputBytes);
                return Convert.ToHexString(hashBytes).Substring(0, 9);
            }
        }

        return variation.GetJsonPropertiesHashCode();
    }

    public static void ClearImageCache()
    {
        Core.Instance.CachedImages.Clear();
        Debug.Log("Cleared image cache");
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
                fileName = titleConfigFileName; //"CustomBentTitle"
                break;
        }

        string baseDir = Path.Combine(UserDataPath, fileName);
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
                    //shownNoNameFileWarning = true;
                    Debug.Log("No bent name file found; name will not be bent");
                    NameRoot = null;
                }
            }
            else if (type == DesignationType.Title)
            {
                HasTitleConfigFile = false;
                if (!shownNoTitleFileWarning)
                {
                    //shownNoTitleFileWarning = true;
                    Debug.Log("No bent title file found; title will not be bent");
                    TitleRoot = null;
                }
            }
        }

        if (File.Exists(fileTemplatePath)) // Detect if the user forgot to remove TEMPLATE from the file name ;)
        {
            if (type == DesignationType.Name && !shownNameTemplateWarning && NameRoot == null)
            {
                shownNameTemplateWarning = true;
                LoggerInstance.Warning("TEMPLATE file detected for bent name");
            }
            else if (type == DesignationType.Title && !shownTitleTemplateWarning && TitleRoot == null)
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
            return null;
        }
        else if (type == DesignationType.Title && !HasTitleConfigFile)
        {
            return null;
        }

        Root root = new Root();
        string jsonString = string.Empty;

        if (type == DesignationType.Name)
        {
            jsonString = File.ReadAllText(Path.Combine(UserDataPath, nameConfigFileName + ".json"));
        }
        else if (type == DesignationType.Title)
        {
            jsonString = File.ReadAllText(Path.Combine(UserDataPath, titleConfigFileName + ".json"));
        }

        try
        {
            root = JsonConvert.DeserializeObject<Root>(jsonString);
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
            CachedFailedTexture = RumbleModdingAPI.RMAPI.AssetBundles.LoadAssetFromStream<Texture2D>(this, "NameBending.assets.namebending", "FailedTexture");
            CachedLoadingFrames = HelperFunctions.ConvertGifToList(RumbleModdingAPI.RMAPI.AssetBundles.LoadAssetFromStream<TextAsset>(this, "NameBending.assets.namebending", "LoadingGif").bytes);

            CachedLoadingTexture.hideFlags = HideFlags.HideAndDontSave;
            CachedCensoredTexture.hideFlags = HideFlags.HideAndDontSave;
            CachedFailedTexture.hideFlags = HideFlags.HideAndDontSave;
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

    public IEnumerator AddLocalProp(string type, String text)
    {
        if (!PhotonNetwork.InRoom)
            yield break;

        // Raise event
        if (!Core.Instance.EventRaised && Core.IsInMatch)
        {
            string strToSend = $"NameBending.{type.Substring(0, 1)}|{text}";
            //PhotonNetwork.RaiseEvent(Core.EventNumber, strToSend, new RaiseEventOptions() { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
            Core.Instance.EventRaised = true;
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

            local = HelperFunctions.FindPhotonPlayerFromRumblePlayer(PlayerManager.Instance?.LocalPlayer);
            if (local == null)
            {
                yield return new WaitForSeconds(0.2f);
                tries++;
            }
        }
        Il2CppExitGames.Client.Photon.Hashtable prop = new();

        prop["NameBending." + type] = (Il2CppSystem.Object)text;
        local.SetCustomProperties(prop);
    }

    public IEnumerator AddLocalProp(string type, Variation variation)
    {
        MelonCoroutines.Start(AddLocalProp(type, variation?.SerializedJson ?? "None"));
        yield break;
    }

    public void EventReceived(EventData photonEvent)
    {
        if (photonEvent.Code != Core.EventNumber) return;
        if (!photonEvent.CustomData.ToString().StartsWith("NameBending.")) return;

        string type = photonEvent.CustomData.ToString().Substring(12, 1);
        string json = photonEvent.CustomData.ToString().Substring(14);

        if (type == "N")
            Core.OpponentPhotonName = json;

        else if (type == "T")
            Core.OpponentPhotonTitle = json;
    }

    public void CreatePlatePreview()
    {
        if (ModParent == null)
            ModParent = new GameObject("NameBending");

        CanvasObject = new GameObject("NameplateCanvas");
        CanvasObject.transform.SetParent(ModParent.transform);
        Canvas canvas = CanvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler canvasScaler = CanvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        CanvasObject.AddComponent<GraphicRaycaster>();

        localNameplateImageObject = new GameObject("LocalNameplateImage");
        localNameplateImageObject.transform.SetParent(CanvasObject.transform, false);
        RawImage rawImage = localNameplateImageObject.AddComponent<RawImage>();

        float squareSize = Mathf.Min(Screen.width, Screen.height) / 1.5f;

        RectTransform rectTransform = localNameplateImageObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1, 1);
        rectTransform.anchorMax = new Vector2(1, 1);
        rectTransform.pivot = new Vector2(1, 1);
        rectTransform.anchoredPosition = new Vector2(-10, 200);
        rectTransform.sizeDelta = new Vector2(squareSize, squareSize);

        GameObject localNameplateCamera = new GameObject("LocalNameplateCamera");
        localNameplateCamera.transform.SetParent(ModParent.transform);
        Camera meshCamera = localNameplateCamera.AddComponent<Camera>();
        meshCamera.clearFlags = CameraClearFlags.SolidColor;
        meshCamera.backgroundColor = new Color(0, 0, 0, 0);
        meshCamera.cullingMask = LayerMask.GetMask("UI");

        RenderTexture renderTexture = new RenderTexture(900, 900, 16);
        renderTexture.Create();
        meshCamera.targetTexture = renderTexture;
        meshCamera.orthographic = false;
        meshCamera.transform.position = new Vector3(0, -1000, 1);
        meshCamera.transform.rotation = Quaternion.Euler(0, 180, 0);

        LocalRawNameplateImage = localNameplateImageObject.GetComponent<RawImage>();
        LocalRawNameplateImage.texture = renderTexture;

        if (LocalNameplateClone == null)
        {
            GameObject nameplate = PlayerManager.Instance.LocalPlayer.Controller.transform.Find("NameTag").gameObject;
            LocalNameplateClone = CloneNameplate(nameplate, true);
            LocalNameplateClone.transform.SetParent(ModParent.transform);
            LocalNameplateClone.active = true;
            LocalNameplateClone.transform.position = new Vector3(0, -1000, 0);
        }
    }

    public GameObject CloneNameplate(GameObject nameplate, bool isUI = false)
    {
        GameObject newPlate = GameObject.Instantiate(nameplate);
        newPlate.SetActive(true);

        PlayerNameTag nameTag = newPlate.GetComponent<PlayerNameTag>();

        if (nameTag.parentController == null)
            nameTag.parentController = nameplate.GetComponentInParent<PlayerController>();
        if (nameTag.parentController == null)
            nameTag.parentController = nameTag.followTarget.GetComponentInParent<PlayerController>();
        if (nameTag.parentController == null)
            nameTag.parentController = PlayerManager.Instance.LocalPlayer.Controller;

        PlayerController owner = nameTag.parentController;
        bool isLocal = owner.controllerType == Il2CppRUMBLE.Players.ControllerType.Local;
        nameTag.followTarget = null;
        nameTag.ChangeOpacity(1f);
        nameTag.RefreshNameTag();
        foreach (NameBend component in newPlate.GetComponentsInChildren<NameBend>())
        {
            component.ResetAll();
            GameObject.DestroyImmediate(component);
        }

        MelonCoroutines.Start(Core.Instance.ApplyComponentsToPlate(newPlate, owner.assignedPlayer, false, isLocal, isUI));

        if (isUI)
        {
            foreach (Transform obj in newPlate.GetComponentsInChildren<Transform>())
                obj.gameObject.layer = LayerMask.NameToLayer("UI");
        }

        return newPlate;
    }

    public void SetPlatePreview(bool enabled)
    {
        if (!globalInit) return;
        if (Config.DisableMod.Value) enabled = false;
        MelonCoroutines.Start(_());

        IEnumerator _()
        {
            while (PlayerManager.Instance?.LocalPlayer?.Controller == null)
                yield return new WaitForSeconds(0.25f);

            if (CanvasObject == null && enabled) CreatePlatePreview();

            if (CanvasObject != null)
            {
                CanvasObject.SetActive(enabled);
                foreach (NameBend component in LocalNameplateClone.GetComponentsInChildren<NameBend>())
                {
                    component.OnUpdateDesignations();
                }
            }
        }
    }
}
