using MelonLoader;
using System;
using System.IO;
using UIFramework;
using UnityEngine;

namespace NameBending;

public static class Config
{
    public static string ConfigFilePath => Path.Combine(Core.UserDataPath, "UIConfig.cfg");

    public static KeyCode _refreshHotkeyCode = KeyCode.None;
    public static KeyCode RefreshHotkeyCode
    {
        get
        {
            if (!String.IsNullOrEmpty(RefreshHotkey.Value))
            {
                if (_refreshHotkeyCode == KeyCode.None)
                {
                    _refreshHotkeyCode = parseKey(RefreshHotkey.Value);
                }
                return _refreshHotkeyCode;
            }
            
            return KeyCode.None;
        }
    }

    public static bool prevDisableMod = false;
    public static bool prevBentName = false;
    public static bool prevBentTitle = false;
    public static bool prevImages = false;
    public static int prevForceVariation = -1;
    public static bool prevTrustAll = false;

    public static UIFModel.ModelMod Me;

    public static MelonPreferences_Category Cat_NamePlate;
    public static MelonPreferences_Entry<bool> NameplatePreview;
    public static MelonPreferences_Entry<string> RefreshHotkey;

    public static MelonPreferences_Category Cat_SimpleConfig;
    public static MelonPreferences_Entry<bool> EnableSimpleConfig;
    public static MelonPreferences_Entry<string> SimpleNameBend;
    public static MelonPreferences_Entry<string> SimpleTitleBend;
    public static MelonPreferences_Entry<string> SimpleAltText;

    public static MelonPreferences_Category Cat_Toggles;
    public static MelonPreferences_Entry<bool> DisableMod;
    public static MelonPreferences_Entry<bool> MatchInfoPlates;
    public static MelonPreferences_Entry<bool> MyAltName;
    public static MelonPreferences_Entry<bool> MyBentName;
    public static MelonPreferences_Entry<bool> OtherBentNames;
    public static MelonPreferences_Entry<bool> MyBentTitle;
    public static MelonPreferences_Entry<bool> OtherBentTitles;
    public static MelonPreferences_Entry<bool> Animations;
    public static MelonPreferences_Entry<bool> Images;

    public static MelonPreferences_Category Cat_Advanced;
    public static MelonPreferences_Entry<int> TruncationLength;
    public static MelonPreferences_Entry<int> FieldDepthLimit;
    public static MelonPreferences_Entry<int> ForceVariationAt;
    public static MelonPreferences_Entry<float> MipmapBias;
    public static MelonPreferences_Entry<bool> SaveNamesToFiles;
    public static MelonPreferences_Entry<bool> TrustAllImages;
    public static MelonPreferences_Entry<bool> Disclaimer;

    public static void SetUpUI()
    {
        Cat_NamePlate = MelonPreferences.CreateCategory("NamePlate", "Name Plate");
        Cat_NamePlate.SetFilePath(ConfigFilePath);
        NameplatePreview = Cat_NamePlate.CreateEntry("NameplatePreview", false, "Nameplate Preview", "Show a preview of your nameplate on the screen");
        UI.CreateButtonEntry(Cat_NamePlate, "Refresh", "Refresh Designations", "Update your name and title for everyone", Core.Instance.UpdateDesignations);
        RefreshHotkey = Cat_NamePlate.CreateEntry("RefreshHotkey", "N", "Refresh Hotkey", "Keyboard button to refresh with\nLeave empty to disable");

        Cat_SimpleConfig = MelonPreferences.CreateCategory("SimpleConfig", "Simple Config");
        Cat_SimpleConfig.SetFilePath(ConfigFilePath);
        EnableSimpleConfig = Cat_SimpleConfig.CreateEntry("EnableSimpleConfig", true, "Enable Simple Config", "Enable a simple way to set name, title, and alt text\n" +
                                                                                                              "Supports Unity rich text tags, such as colors, e.g. <noparse><#F00></noparse>\n" +
                                                                                                              "Advanced features such as animations and images will require configuring the .json files in UserData/NameBending");
        SimpleNameBend = Cat_SimpleConfig.CreateEntry("SimpleNameBend", "", "Custom Bent Name", "When set, your name will appear like this to others who have the mod\nAdvanced features such as animations and images will require configuring the .json files in UserData/NameBending");
        SimpleTitleBend = Cat_SimpleConfig.CreateEntry("SimpleTitleBend", "", "Custom Bent Title", "When set, your title will appear like this to others who have the mod");
        SimpleAltText = Cat_SimpleConfig.CreateEntry("SimpleAltText", "", "Alt Text", $"When set, your name will appear like this to others who <b>do not</b> have the mod\nLimited to {Core.AltTextCharLimit} characters");

        Cat_Toggles = MelonPreferences.CreateCategory("Toggles", "Toggles");
        Cat_Toggles.SetFilePath(ConfigFilePath);
        DisableMod = Cat_Toggles.CreateEntry("DisableMod", false, "Disable Mod", "Turn off all name bending");
        MatchInfoPlates = Cat_Toggles.CreateEntry("MatchInfoPlates", true, "MatchInfo Plates", "Show nameplates on the MatchInfo mod board");
        MyAltName = Cat_Toggles.CreateEntry("MyAltName", true, "My Alt Name", "Enable the name that shows to people who don't have the mod");
        MyBentName = Cat_Toggles.CreateEntry("MyBentName", true, "My Bent Name", "Show your custom name to other people");
        OtherBentNames = Cat_Toggles.CreateEntry("OtherBentNames", true, "Other Bent Names", "Show other people's custom names");
        MyBentTitle = Cat_Toggles.CreateEntry("MyBentTitle", true, "My Bent Title", "Show your custom title to other people");
        OtherBentTitles = Cat_Toggles.CreateEntry("OtherBentTitles", true, "Other Bent Titles", "Show other people's custom titles");
        Animations = Cat_Toggles.CreateEntry("Animations", true, "Animations", "Show animations in custom names and titles");
        Images = Cat_Toggles.CreateEntry("Images", true, "Images", "Enable all nameplate images");

        Cat_Advanced = MelonPreferences.CreateCategory("Advanced", "Advanced");
        Cat_Advanced.SetFilePath(ConfigFilePath);
        TruncationLength = Cat_Advanced.CreateEntry("TruncationLength", -1, "Truncation Length", "The number of characters to truncate names to\nSet to -1 to disable");
        FieldDepthLimit = Cat_Advanced.CreateEntry("FieldDepthLimit", 10, "Field Depth Limit", "The amount of times fields are allowed to process other fields within themselves");
        ForceVariationAt = Cat_Advanced.CreateEntry("ForceVariationAt", -1, "ForceVariationAt", "Always pick the variation at this index\n-1 to disable");
        MipmapBias = Cat_Advanced.CreateEntry("MipmapBias", -0.5f, "Mipmap Bias", "The mipmap bias of images on nameplates\nLower numbers make clearer images\n(usually between -2.0 and 2.0)");
        SaveNamesToFiles = Cat_Advanced.CreateEntry("SaveNamesToFiles", false, "Save Names to Files", "Save every name and title you come across to files in\nUserdata/NameBending/saved_names");
        TrustAllImages = Cat_Advanced.CreateEntry("TrustAllImages", false, "<#F00><b>TRUST ALL IMAGES", "<b><#F00>Disable moderation features by automatically trusting images that have not been manually approved by moderators\nRequires accepting the disclaimer below");
        Disclaimer = Cat_Advanced.CreateEntry("Disclaimer", false, "Accept Disclaimer", "I understand that by disabling moderation features, other users can subject me to any image on the internet. These images may be graphic, NSFW, offensive, triggering to photosensitivity, or otherwise unwanted.");
        UI.CreateButtonEntry(Cat_Advanced, "Clear", "Clear Image Cache", "Remove any stored data about existing and previous nameplate images", Core.ClearImageCache);

        Me = UI.Register((MelonBase)Core.Instance, Cat_NamePlate, Cat_SimpleConfig, Cat_Toggles, Cat_Advanced);
        Me.OnModSaved += OnPrefsSaved;
    }

    public static void OnPrefsSaved()
    {
        // MatchInfo Plates
        MatchInfoBoard.SetMatchInfo(Config.MatchInfoPlates.Value);

        // Preview
        Core.Instance.SetPlatePreview(NameplatePreview.Value);

        // Disable Mod
        if (DisableMod.Value)
        {
            foreach (NameBend nameBend in Core.Instance.NameBends)
                nameBend.ResetAll();
        }

        // My Bent Name
        if (!Config.MyBentName.Value || Config.DisableMod.Value)
        {
            foreach (NameBend name in Core.Instance.NameBends)
            {
                if (name == null) continue;
                if (name.DesignationType is Core.DesignationType.Name && name.IsLocal)
                {
                    name.ResetAll();
                }
            }
        }

        // Other Bent Names
        if (!Config.MyBentName.Value || Config.DisableMod.Value)
        {
            foreach (NameBend name in Core.Instance.NameBends)
            {
                if (name == null) continue;
                if (name.DesignationType is Core.DesignationType.Name && !name.IsLocal)
                {
                    name.ResetAll();
                }
            }
        }

        // My Bent Title
        if (!Config.MyBentName.Value || Config.DisableMod.Value)
        {
            foreach (NameBend name in Core.Instance.NameBends)
            {
                if (name == null) continue;
                if (name.DesignationType is Core.DesignationType.Title && name.IsLocal)
                {
                    name.ResetAll();
                }
            }
        }
        // Other Bent Names
        if (!Config.MyBentName.Value || Config.DisableMod.Value)
        {
            foreach (NameBend name in Core.Instance.NameBends)
            {
                if (name == null) continue;
                if (name.DesignationType is Core.DesignationType.Title && !name.IsLocal)
                {
                    name.ResetAll();
                }
            }
        }

        // Simple Alt Text
        if (Config.EnableSimpleConfig.Value && Config.SimpleAltText.Value.Length > Core.AltTextCharLimit)
            Debug.Error($"Alt text cannot be more than {Core.AltTextCharLimit} characters");

        // Images
        if (!Config.Images.Value || Config.DisableMod.Value)
        {
            foreach (BentImage image in Core.Instance.BentImages)
            {
                if (image == null) continue;
                image.ImageInfo.UndownloadImage();
                image.Reset();
            }
        }

        // Refresh Hotkey
        _refreshHotkeyCode = KeyCode.None;

        // Force Variation At
        if (ForceVariationAt.Value != prevForceVariation)
        {
            Core.Instance.UpdateDesignations();
        }
        prevForceVariation = ForceVariationAt.Value;

        // Mipmap Bias
        foreach (BentImage image in Core.Instance.BentImages)
        {
            if (image == null) continue;
                image.SetMipmapBias(Config.MipmapBias.Value);
        }

        // Trust All Images
        if (prevTrustAll != (Config.TrustAllImages.Value && Config.Disclaimer.Value))
            Core.ClearImageCache();
        prevTrustAll = Config.TrustAllImages.Value && Config.Disclaimer.Value;

        if (Config.TrustAllImages.Value && Config.Disclaimer.Value)
        {
            foreach (BentImage image in Core.Instance.BentImages)
            {
                if (image == null) continue;
                image.Uncensor();
            }
        }
        else
        {
            foreach (BentImage image in Core.Instance.BentImages)
            {
                if (image == null) continue;
                image.CensorIfNeeded();
            }
        }

        if (prevDisableMod && !DisableMod.Value
         || !prevBentName && MyBentName.Value
         || !prevBentTitle && MyBentTitle.Value
         || !prevImages && Images.Value)
        {
            foreach (NameBend name in Core.Instance.NameBends)
                name.OnUpdateDesignations();
        }
    }

    static KeyCode parseKey(string key)
    {
        if (Enum.TryParse<KeyCode>(key, true, out KeyCode keyCode))
        {
            return keyCode;
        }
        else
        {
            return KeyCode.None;
        }
    }
}