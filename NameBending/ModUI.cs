using RumbleModUI;

namespace NameBending
{
    partial class Core
    {
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
            Mod.AddToList("<#B09>Images", true, 0, "See images in bent names", new Tags());
            Mod.AddToList("<#B09>Truncation Length", -1, "Max number of characters to show on names and titles\n(set to -1 to disable truncation)", new Tags());
            Mod.AddToList("<#888>MatchInfo Name Plates", true, 0, "Show fully customized name plates on the MatchInfo sign", new Tags());
            Mod.AddToList("<#888>Save Names to Files", true, 0, "Saves any name or title you come across to UserData/NameBending/saved_names", new Tags());
            Mod.AddToList("<#888>Disable Refresh Shortcut", false, 0, "Disables pressing N to update names and titles", new Tags());

            Mod.GetFromFile();
            Mod.ModSaved += OnUISave;

            UI.instance.AddMod(Mod);
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
            public static bool Images => (bool)Core.Instance.Mod.Settings[8].SavedValue;
            public static int TruncationLength => (int)Core.Instance.Mod.Settings[9].SavedValue;
            public static bool MatchInfoNamePlates => (bool)Core.Instance.Mod.Settings[10].SavedValue;
            public static bool SaveNamesToFiles => (bool)Core.Instance.Mod.Settings[11].SavedValue;
            public static bool DisableRefreshShortcut => (bool)Core.Instance.Mod.Settings[12].SavedValue;
        }

        public void OnUISave()
        {

        }
    }
}
