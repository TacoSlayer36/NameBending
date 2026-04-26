using MelonLoader;
using ModLoader;
using System;
using System.Drawing;
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

    public static UIFModel.ModelMod Me;

    public static MelonPreferences_Category Cat_NamePlate;
    public static MelonPreferences_Entry<bool> NameplatePreview;
    public static UIFModel.ButtonEntry UpdateDesignations;
    public static MelonPreferences_Entry<string> RefreshHotkey;

    public static MelonPreferences_Category Cat_SimpleConfig;
    // TODO

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
    public static MelonPreferences_Entry<float> MipmapBias;
    public static MelonPreferences_Entry<bool> SaveNamesToFiles;
    public static MelonPreferences_Entry<bool> TrustAllImages;
    public static MelonPreferences_Entry<bool> Disclaimer;
    public static UIFModel.ButtonEntry ClearImageCache;

    public static void SetUpUI()
    {
        Cat_NamePlate = MelonPreferences.CreateCategory("NamePlate", "Name Plate");
        Cat_NamePlate.SetFilePath(ConfigFilePath);
        NameplatePreview = Cat_NamePlate.CreateEntry("NameplatePreview", false, "Nameplate Preview", "Show a preview of your nameplate on the screen");
        UpdateDesignations = new(Core.UpdateDesignations, "UpdateDesignations", "Update your name and title for everyone", "Refresh");
        RefreshHotkey = Cat_NamePlate.CreateEntry("RefreshHotkey", "N", "Refresh Hotkey", "Keyboard button to refresh with\nLeave empty to disable");

        Cat_Toggles= MelonPreferences.CreateCategory("Toggles", "Toggles");
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
        MipmapBias = Cat_Advanced.CreateEntry("MipmapBias", -0.5f, "Mipmap Bias", "The mipmap bias of images on nameplates\nLower numbers make clearer images\n(usually between -2.0 and 2.0)");
        SaveNamesToFiles = Cat_Advanced.CreateEntry("SaveNamesToFiles", false, "Save Names to Files", "Save every name and title you come across to files in\nUserdata/NameBending/saved_names");
        TrustAllImages = Cat_Advanced.CreateEntry("TrustAllImages", false, "<#F00><b>TRUST ALL IMAGES", "<b><#F00>Disable moderation features by automatically trusting images that have not been manually approved by moderators\nRequires accepting the disclaimer below");
        Disclaimer = Cat_Advanced.CreateEntry("Disclaimer", false, "Accept Disclaimer", "I understand that by disabling moderation features, other users can subject me to any image on the internet. These images may be graphic, NSFW, offensive, triggering to photosensitivity, or otherwise unwanted.");
        ClearImageCache = new(Core.ClearImageCache, "ClearImageCache", "Remove any stored data about existing and previous nameplate images", "Clear Image Cache");

        Me = UI.Register((MelonBase)Core.Instance, Cat_NamePlate, Cat_Toggles, Cat_Advanced);
        Me.OnModSaved += OnModSaved;

        ((UIFModel.ModelCategoryItem)Me.GetSubmodel("NamePlate"))
            .AddSubmodel(UpdateDesignations);

        ((UIFModel.ModelCategoryItem)Me.GetSubmodel("Advanced"))
            .AddSubmodel(ClearImageCache);
    }

    public static void OnModSaved()
    {
        // TODO: MatchInfo Plates

        // Preview
        Core.Instance.SetPlatePreview(NameplatePreview.Value);

        // My Bent Name
        if (!Config.MyBentName.Value || Config.DisableMod.Value)
        {
            foreach (NameBend name in Core.Instance.NameBends)
            {
                if (name == null) return;
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
                if (name == null) return;
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

        // Images
        if (!Config.Images.Value || Config.DisableMod.Value)
        {
            foreach (BentImage image in Core.Instance.BentImages)
            {
                if (image == null) continue;
                image.Reset();
                image.ImageInfo.UndownloadImage();
            }
        }

        // Refresh Hotkey
        _refreshHotkeyCode = KeyCode.None;

        // Mipmap Bias
        foreach (BentImage image in Core.Instance.BentImages)
        {
            if (image == null) return;
                image.SetMipmapBias(Config.MipmapBias.Value);
        }

        // Trust All Images
        if (Config.TrustAllImages.Value && Config.DisableMod.Value)
        {
            foreach (BentImage image in Core.Instance.BentImages)
            {
                if (image == null) return;
                image.Uncensor();
            }
        }
        else
        {
            foreach (BentImage image in Core.Instance.BentImages)
            {
                if (image == null) return;
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
        if (Enum.TryParse(key, out KeyCode keyCode))
        {
            return keyCode;
        }
        else
        {
            return KeyCode.None;
        }
    }
}