#nullable enable
using Barotrauma.Extensions;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.IO;
#if CLIENT
using Barotrauma.ClientSource.Settings;
using Barotrauma.Networking;
using Microsoft.Xna.Framework.Input;
#endif

namespace Barotrauma
{
    public enum WindowMode
    {
        Windowed, Fullscreen, BorderlessWindowed
    }

    public enum LosMode
    {
        None = 0,
        Transparent = 1,
        Opaque = 2
    }

    public enum VoiceMode
    {
        Disabled,
        PushToTalk,
        Activity
    }

    public static class GameSettings
    {
        public struct Config
        {
            public static Config GetDefault()
            {
                Config config = new Config
                {
                    Language = TextManager.DefaultLanguage,
                    SubEditorUndoBuffer = 32,
                    MaxAutoSaves = 8,
                    AutoSaveIntervalSeconds = 300,
                    SubEditorBackground = new Color(13, 37, 69, 255),
                    EnableSplashScreen = true,
                    PauseOnFocusLost = true,
                    AimAssistAmount = 0.5f,
                    RunByDefault = true,
                    EnableMouseLook = false,
                    ChatOpen = true,
                    CrewMenuOpen = true,
                    CampaignDisclaimerShown = false,
                    EditorDisclaimerShown = false,
                    ShowOffensiveServerPrompt = true,
                    TutorialSkipWarning = true,
                    CorpseDespawnDelay = 600,
                    CorpsesPerSubDespawnThreshold = 5,
                    #if OSX
                    UseDualModeSockets = false,
                    #else
                    UseDualModeSockets = true,
                    #endif
                    DisableInGameHints = false,
                    EnableSubmarineAutoSave = true,
                    Graphics = GraphicsSettings.GetDefault(),
                    Audio = AudioSettings.GetDefault(),
#if CLIENT
                    KeyMap = KeyMapping.GetDefault(),
                    InventoryKeyMap = InventoryKeyMapping.GetDefault()
#endif

                };
#if DEBUG
                config.UseSteamMatchmaking = true;
                config.QuickStartSub = "Humpback".ToIdentifier();
                config.RequireSteamAuthentication = true;
                config.AutomaticQuickStartEnabled = false;
                config.AutomaticCampaignLoadEnabled = false;
                config.TextManagerDebugModeEnabled = false;
                config.ModBreakerMode = false;
#endif
                return config;
            }

            public static Config FromFile(string configFile, in Config? fallback = null)
            {
                XDocument doc = XMLExtensions.TryLoadXml(configFile);

                return FromElement(doc.Root ?? throw new InvalidOperationException("Unable to load config file: XML document is null."), fallback);
            }

            public static Config FromElement(XElement element, in Config? fallback = null)
            {
                Config retVal = fallback ?? GetDefault();
                
                retVal.DeserializeElement(element);
                if (retVal.Language == LanguageIdentifier.None)
                {
                    retVal.Language = TextManager.DefaultLanguage;
                }

                retVal.Graphics = GraphicsSettings.FromElements(element.GetChildElements("graphicsmode", "graphicssettings"), retVal.Graphics);
                retVal.Audio = AudioSettings.FromElements(element.GetChildElements("audio"), retVal.Audio);
#if CLIENT
                retVal.KeyMap = new KeyMapping(element.GetChildElements("keymapping"), retVal.KeyMap);
                retVal.InventoryKeyMap = new InventoryKeyMapping(element.GetChildElements("inventorykeymapping"), retVal.InventoryKeyMap);
#endif

                return retVal;
            }

            public LanguageIdentifier Language;
            public bool VerboseLogging;
            public bool SaveDebugConsoleLogs;
            public int SubEditorUndoBuffer;
            public int MaxAutoSaves;
            public int AutoSaveIntervalSeconds;
            public Color SubEditorBackground;
            public bool EnableSplashScreen;
            public bool PauseOnFocusLost;
            public float AimAssistAmount;
            public bool RunByDefault;
            public bool EnableMouseLook;
            public bool ChatOpen;
            public bool CrewMenuOpen;
            public bool CampaignDisclaimerShown;
            public bool EditorDisclaimerShown;
            public bool ShowOffensiveServerPrompt;
            public bool TutorialSkipWarning;
            public int CorpseDespawnDelay;
            public int CorpsesPerSubDespawnThreshold;
            public bool UseDualModeSockets;
            public bool DisableInGameHints;
            public bool EnableSubmarineAutoSave;
            public Identifier QuickStartSub;
#if DEBUG
            public bool UseSteamMatchmaking;
            public bool RequireSteamAuthentication;
            public bool AutomaticQuickStartEnabled;
            public bool AutomaticCampaignLoadEnabled;
            public bool TestScreenEnabled;
            public bool TextManagerDebugModeEnabled;
            public bool ModBreakerMode;
#else
            public bool UseSteamMatchmaking => true;
            public bool RequireSteamAuthentication => true;
#endif

            public struct GraphicsSettings
            {
                public static readonly Point MinSupportedResolution = new Point(1024, 540);
                
                public static GraphicsSettings GetDefault()
                {
                    GraphicsSettings gfxSettings = new GraphicsSettings
                    {
                        RadialDistortion = true,
                        InventoryScale = 1.0f,
                        LightMapScale = 1.0f,
                        TextScale = 1.0f,
                        HUDScale = 1.0f,
                        Specularity = true,
                        ChromaticAberration = true,
                        ParticleLimit = 1500,
                        LosMode = LosMode.Transparent
                    };
                    gfxSettings.RadialDistortion = true;
                    gfxSettings.CompressTextures = true;
                    gfxSettings.FrameLimit = 300;
                    gfxSettings.VSync = true;
#if DEBUG
                    gfxSettings.DisplayMode = WindowMode.Windowed;
#else
                    gfxSettings.DisplayMode = WindowMode.BorderlessWindowed;
#endif
                    return gfxSettings;
                }

                public static GraphicsSettings FromElements(IEnumerable<XElement> elements, in GraphicsSettings? fallback = null)
                {
                    GraphicsSettings retVal = fallback ?? GetDefault();
                    elements.ForEach(element => retVal.DeserializeElement(element));
                    return retVal;
                }

                public int Width;
                public int Height;
                public bool VSync;
                public bool CompressTextures;
                public int FrameLimit;
                public WindowMode DisplayMode;
                public int ParticleLimit;
                public bool Specularity;
                public bool ChromaticAberration;
                public LosMode LosMode;
                public float HUDScale;
                public float InventoryScale;
                public float LightMapScale;
                public float TextScale;
                public bool RadialDistortion;
            }

            [StructSerialization.Skip]
            public GraphicsSettings Graphics;

            public struct AudioSettings
            {
                public static class DeviceNameHandler
                {
                    public static string Read(string s)
                        => System.Xml.XmlConvert.DecodeName(s)!;

                    public static string Write(string s)
                        => System.Xml.XmlConvert.EncodeName(s)!;
                }
                
                public static AudioSettings GetDefault()
                {
                    AudioSettings audioSettings = new AudioSettings
                    {
                        MusicVolume = 0.3f,
                        SoundVolume = 0.5f,
                        VoiceChatVolume = 0.5f,
                        VoiceChatCutoffPrevention = 0,
                        MicrophoneVolume = 5,
                        MuteOnFocusLost = false,
                        DynamicRangeCompressionEnabled = true,
                        UseDirectionalVoiceChat = true,
                        VoipAttenuationEnabled = true,
                        VoiceSetting = VoiceMode.PushToTalk,
                        UseLocalVoiceByDefault = false,
                        DisableVoiceChatFilters = false
                    };
                    return audioSettings;
                }

                public static AudioSettings FromElements(IEnumerable<XElement> elements, in AudioSettings? fallback = null)
                {
                    AudioSettings retVal = fallback ?? GetDefault();
                    elements.ForEach(element => retVal.DeserializeElement(element));
                    return retVal;
                }

                public float MusicVolume;
                public float SoundVolume;
                public float VoiceChatVolume;
                public int VoiceChatCutoffPrevention;
                public float MicrophoneVolume;
                public bool MuteOnFocusLost;
                public bool DynamicRangeCompressionEnabled;
                public bool UseDirectionalVoiceChat;
                public bool VoipAttenuationEnabled;
                public VoiceMode VoiceSetting;
                
                [StructSerialization.Handler(typeof(DeviceNameHandler))]
                public string AudioOutputDevice;
                [StructSerialization.Handler(typeof(DeviceNameHandler))]
                public string VoiceCaptureDevice;
                
                public float NoiseGateThreshold;
                public bool UseLocalVoiceByDefault;
                public bool DisableVoiceChatFilters;
            }

            [StructSerialization.Skip]
            public AudioSettings Audio;

#if CLIENT
            public struct KeyMapping
            {
                private readonly static ImmutableDictionary<InputType, KeyOrMouse> DefaultsQwerty =
                    new Dictionary<InputType, KeyOrMouse>()
                    {
                        { InputType.ToggleWalkOrRun, Keys.LeftShift },
                        { InputType.Attack, Keys.R },
                        { InputType.Crouch, Keys.LeftControl },
                        { InputType.Grab, Keys.G },
                        { InputType.MoveBackwards, Keys.LeftAlt },
                        { InputType.Health, Keys.H },
                        { InputType.Ragdoll, Keys.Space },
                        { InputType.Aim, MouseButton.SecondaryMouse },

                        { InputType.InfoTab, Keys.Tab },
                        { InputType.Chat, Keys.T },
                        { InputType.RadioChat, Keys.R },
                        { InputType.CrewOrders, Keys.C },

                        { InputType.Voice, Keys.V },
                        { InputType.LocalVoice, Keys.B },
                        { InputType.Command, MouseButton.MiddleMouse },
                        { InputType.PreviousFireMode, MouseButton.MouseWheelDown },
                        { InputType.NextFireMode, MouseButton.MouseWheelUp },

                        { InputType.TakeHalfFromInventorySlot, Keys.LeftShift },
                        { InputType.TakeOneFromInventorySlot, Keys.LeftControl },

                        { InputType.Up, Keys.W },
                        { InputType.Down, Keys.S },
                        { InputType.Left, Keys.A },
                        { InputType.Right, Keys.D },
                        { InputType.ToggleInventory, Keys.Q },

                        { InputType.SelectNextCharacter, Keys.Z },
                        { InputType.SelectPreviousCharacter, Keys.X },

                        { InputType.Use, Keys.E },
                        { InputType.Select, MouseButton.PrimaryMouse },
                        { InputType.Deselect, MouseButton.SecondaryMouse },
                        { InputType.Shoot, MouseButton.PrimaryMouse }
                }.ToImmutableDictionary();

                public static KeyMapping GetDefault() => new KeyMapping
                {
                    Bindings = DefaultsQwerty
                        .Select(kvp =>
                            (kvp.Key, kvp.Value.MouseButton == MouseButton.None
                                ? (KeyOrMouse)Keyboard.QwertyToCurrentLayout(kvp.Value.Key)
                                : (KeyOrMouse)kvp.Value.MouseButton))
                        .ToImmutableDictionary()
                };

                public KeyMapping(IEnumerable<XElement> elements, in KeyMapping? fallback)
                {
                    var defaultBindings = GetDefault().Bindings;
                    Dictionary<InputType, KeyOrMouse> bindings = fallback?.Bindings?.ToMutable() ?? defaultBindings.ToMutable();
                    foreach (InputType inputType in (InputType[])Enum.GetValues(typeof(InputType)))
                    {
                        if (!bindings.ContainsKey(inputType)) { bindings.Add(inputType, defaultBindings[inputType]); }
                    }

                    foreach (XElement element in elements)
                    {
                        foreach (XAttribute attribute in element.Attributes())
                        {
                            if (Enum.TryParse(attribute.Name.LocalName, out InputType result))
                            {
                                bindings[result] = element.GetAttributeKeyOrMouse(attribute.Name.LocalName, bindings[result]);
                            }
                        }
                    }

                    Bindings = bindings.ToImmutableDictionary();
                }

                public KeyMapping WithBinding(InputType type, KeyOrMouse bind)
                {
                    KeyMapping newMapping = this;
                    newMapping.Bindings = newMapping.Bindings
                        .Select(kvp =>
                            kvp.Key == type
                                ? (type, bind)
                                : (kvp.Key, kvp.Value))
                        .ToImmutableDictionary();
                    return newMapping;
                }
                
                public ImmutableDictionary<InputType, KeyOrMouse> Bindings;

                public LocalizedString KeyBindText(InputType inputType) => Bindings[inputType].Name;
            }

            [StructSerialization.Skip]
            public KeyMapping KeyMap;

            public struct InventoryKeyMapping
            {
                public ImmutableArray<KeyOrMouse> Bindings;

                public static InventoryKeyMapping GetDefault() => new InventoryKeyMapping
                {
                    Bindings = new KeyOrMouse[]
                    {
                        Keys.D1,
                        Keys.D2,
                        Keys.D3,
                        Keys.D4,
                        Keys.D5,
                        Keys.D6,
                        Keys.D7,
                        Keys.D8,
                        Keys.D9,
                        Keys.D0,
                    }.ToImmutableArray()
                };

                public InventoryKeyMapping WithBinding(int index, KeyOrMouse keyOrMouse)
                {
                    var thisBindings = Bindings;
                    return new InventoryKeyMapping()
                    {
                        Bindings = Enumerable.Range(0, thisBindings.Length)
                            .Select(i => i == index ? keyOrMouse : thisBindings[i])
                            .ToImmutableArray()
                    };
                }

                public InventoryKeyMapping(IEnumerable<XElement> elements, InventoryKeyMapping? fallback)
                {
                    var bindings = (fallback?.Bindings ?? GetDefault().Bindings).ToArray();
                    foreach (XElement element in elements)
                    {
                        for (int i = 0; i < bindings.Length; i++)
                        {
                            bindings[i] = element.GetAttributeKeyOrMouse($"slot{i}", bindings[i]);
                        }
                    }
                    Bindings = bindings.ToImmutableArray();
                }
            }

            [StructSerialization.Skip]
            public InventoryKeyMapping InventoryKeyMap;
#endif
        }

        public const string PlayerConfigPath = "config_player.xml";

        private static Config currentConfig;
        public static ref readonly Config CurrentConfig => ref currentConfig;

        public static void Init()
        {
            XDocument? currentConfigDoc = null;

            if (File.Exists(PlayerConfigPath))
            {
                currentConfigDoc = XMLExtensions.TryLoadXml(PlayerConfigPath);
            }

            if (currentConfigDoc != null)
            {
                currentConfig = Config.FromElement(currentConfigDoc.Root ?? throw new NullReferenceException("Config XML element is invalid: document is null."));
#if CLIENT
                ServerListFilters.Init(currentConfigDoc.Root.GetChildElement("serverfilters"));
                MultiplayerPreferences.Init(
                    currentConfigDoc.Root.GetChildElement("player"),
                    currentConfigDoc.Root.GetChildElement("gameplay")?.GetChildElement("jobpreferences"));
                IgnoredHints.Init(currentConfigDoc.Root.GetChildElement("ignoredhints"));
                DebugConsoleMapping.Init(currentConfigDoc.Root.GetChildElement("debugconsolemapping"));
                CompletedTutorials.Init(currentConfigDoc.Root.GetChildElement("tutorials"));
#endif
            }
            else
            {
                currentConfig = Config.GetDefault();
                SaveCurrentConfig();
            }
        }

        public static void SetCurrentConfig(Config newConfig)
        {
            bool setGraphicsMode =
                currentConfig.Graphics.Width != newConfig.Graphics.Width
                || currentConfig.Graphics.Height != newConfig.Graphics.Height
                || currentConfig.Graphics.VSync != newConfig.Graphics.VSync
                || currentConfig.Graphics.DisplayMode != newConfig.Graphics.DisplayMode;

            bool languageChanged = currentConfig.Language != newConfig.Language;

            bool audioOutputChanged = currentConfig.Audio.AudioOutputDevice != newConfig.Audio.AudioOutputDevice;
            bool voiceCaptureChanged = currentConfig.Audio.VoiceCaptureDevice != newConfig.Audio.VoiceCaptureDevice;

            currentConfig = newConfig;

#if CLIENT
            if (languageChanged) 
            { 
                TextManager.ClearCache(); 
                GameMain.Instance.ApplyGraphicsSettings();
            }

            if (setGraphicsMode)
            {
                GameMain.Instance.ApplyGraphicsSettings();
            }

            if (audioOutputChanged)
            {
                GameMain.SoundManager?.InitializeAlcDevice(currentConfig.Audio.AudioOutputDevice);
            }

            if (voiceCaptureChanged)
            {
                VoipCapture.ChangeCaptureDevice(currentConfig.Audio.VoiceCaptureDevice);
            }
            GameMain.SoundManager?.ApplySettings();
#endif
        }

        public static void SaveCurrentConfig()
        {
            XDocument configDoc = new XDocument();
            XElement root = new XElement("config"); configDoc.Add(root);
            currentConfig.SerializeElement(root);

            XElement graphicsElement = new XElement("graphicssettings"); root.Add(graphicsElement);
            currentConfig.Graphics.SerializeElement(graphicsElement);
            
#region Backwards compatibility crap
#warning TODO: remove once modding refactor ships in a stable release
            XElement backwardsCompatibilityGraphicsMode = new XElement(graphicsElement); root.Add(backwardsCompatibilityGraphicsMode);
            backwardsCompatibilityGraphicsMode.Name = "graphicsmode";
#endregion
            
            XElement audioElement = new XElement("audio"); root.Add(audioElement);
            currentConfig.Audio.SerializeElement(audioElement);

            XElement contentPackagesElement = new XElement("contentpackages"); root.Add(contentPackagesElement);
#region More backwards compatibility crap
            XComment backwardsCompatibleComment = new XComment("Backwards compatibility"); contentPackagesElement.Add(backwardsCompatibleComment);
#warning TODO: remove once modding refactor ships in a stable release
            XElement backwardsCompatibleCoreElement = new XElement("core"); contentPackagesElement.Add(backwardsCompatibleCoreElement);
            backwardsCompatibleCoreElement.SetAttributeValue("name", "Vanilla 0.9");
#endregion
            XComment corePackageComment = new XComment(ContentPackageManager.EnabledPackages.Core?.Name ?? "Vanilla"); contentPackagesElement.Add(corePackageComment);
            XElement corePackageElement = new XElement(ContentPackageManager.CorePackageElementName); contentPackagesElement.Add(corePackageElement);
            corePackageElement.SetAttributeValue("path", ContentPackageManager.EnabledPackages.Core?.Path ?? ContentPackageManager.VanillaFileList);

            XElement regularPackagesElement = new XElement(ContentPackageManager.RegularPackagesElementName); contentPackagesElement.Add(regularPackagesElement);
            foreach (var regularPackage in ContentPackageManager.EnabledPackages.Regular)
            {
                XComment packageComment = new XComment(regularPackage.Name); regularPackagesElement.Add(packageComment);
                XElement packageElement = new XElement(ContentPackageManager.RegularPackagesSubElementName); regularPackagesElement.Add(packageElement);
                packageElement.SetAttributeValue("path", regularPackage.Path);
            }

#if CLIENT
            XElement serverFiltersElement = new XElement("serverfilters"); root.Add(serverFiltersElement);
            ServerListFilters.Instance.SaveTo(serverFiltersElement);

            XElement characterElement = new XElement("player"); root.Add(characterElement);
            MultiplayerPreferences.Instance.SaveTo(characterElement);

            XElement ignoredHintsElement = new XElement("ignoredhints"); root.Add(ignoredHintsElement);
            IgnoredHints.Instance.SaveTo(ignoredHintsElement);

            XElement debugConsoleMappingElement = new XElement("debugconsolemapping"); root.Add(debugConsoleMappingElement);
            DebugConsoleMapping.Instance.SaveTo(debugConsoleMappingElement);
            
            XElement tutorialsElement = new XElement("tutorials"); root.Add(tutorialsElement);
            CompletedTutorials.Instance.SaveTo(tutorialsElement);
            
            XElement keyMappingElement = new XElement("keymapping",
                currentConfig.KeyMap.Bindings.Select(kvp
                    => new XAttribute(kvp.Key.ToString(), kvp.Value.ToString())));
            root.Add(keyMappingElement);

            XElement inventoryKeyMappingElement = new XElement("inventorykeymapping",
                Enumerable.Range(0, currentConfig.InventoryKeyMap.Bindings.Length)
                    .Zip(currentConfig.InventoryKeyMap.Bindings)
                    .Cast<(int Index, KeyOrMouse Bind)>()
                    .Select(kvp
                        => new XAttribute($"slot{kvp.Index.ToString(CultureInfo.InvariantCulture)}", kvp.Bind.ToString())));
            root.Add(inventoryKeyMappingElement);
#endif

            configDoc.SaveSafe(PlayerConfigPath);
            
            System.Xml.XmlWriterSettings settings = new System.Xml.XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = true,
                NewLineOnAttributes = true
            };

            try
            {
                using (var writer = XmlWriter.Create(PlayerConfigPath, settings))
                {
                    configDoc.WriteTo(writer);
                    writer.Flush();
                }
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Saving game settings failed.", e);
                GameAnalyticsManager.AddErrorEventOnce("GameSettings.Save:SaveFailed", GameAnalyticsManager.ErrorSeverity.Error,
                    "Saving game settings failed.\n" + e.Message + "\n" + e.StackTrace.CleanupStackTrace());
            }
        }
    }
}