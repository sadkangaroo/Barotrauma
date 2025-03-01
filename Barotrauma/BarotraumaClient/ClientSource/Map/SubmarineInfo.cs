﻿using Microsoft.Xna.Framework;
using System;
using System.Globalization;
using System.Linq;

namespace Barotrauma
{
    partial class SubmarineInfo : IDisposable
    {
        public Sprite PreviewImage;
        
        partial void InitProjectSpecific()
        {
            string previewImageData = SubmarineElement.GetAttributeString("previewimage", "");
            if (!string.IsNullOrEmpty(previewImageData))
            {
                try
                {
                    using (System.IO.MemoryStream mem = new System.IO.MemoryStream(Convert.FromBase64String(previewImageData)))
                    {
                        var texture = TextureLoader.FromStream(mem, path: FilePath, compress: false);
                        if (texture == null) { throw new Exception("PreviewImage texture returned null"); }
                        PreviewImage = new Sprite(texture, sourceRectangle: null, newOffset: null, path: FilePath);
                    }
                }
                catch (Exception e)
                {
                    DebugConsole.ThrowError("Loading the preview image of the submarine \"" + Name + "\" failed. The file may be corrupted.", e);
                    GameAnalyticsManager.AddErrorEventOnce("Submarine..ctor:PreviewImageLoadingFailed", GameAnalyticsManager.ErrorSeverity.Error,
                        "Loading the preview image of the submarine \"" + Name + "\" failed. The file may be corrupted.");
                    PreviewImage = null;
                }
            }
        }

        public void CreatePreviewWindow(GUIComponent parent)
        {
            var content = new GUIFrame(new RectTransform(Vector2.One, parent.RectTransform), style: null);

            var previewButton = new GUIButton(new RectTransform(new Vector2(1f, 0.5f), content.RectTransform), style: null)
            {
                CanBeFocused = SubmarineElement != null,
                OnClicked = (btn, obj) => { SubmarinePreview.Create(this); return false; },
            };

            var previewImage = PreviewImage ?? savedSubmarines.Find(s => s.Name.Equals(Name, StringComparison.OrdinalIgnoreCase))?.PreviewImage;
            if (previewImage == null)
            {
                new GUITextBlock(new RectTransform(Vector2.One, previewButton.RectTransform), TextManager.Get(SavedSubmarines.Contains(this) ? "SubPreviewImageNotFound" : "SubNotDownloaded"));
            }
            else
            {
                var submarinePreviewBackground = new GUIFrame(new RectTransform(Vector2.One, previewButton.RectTransform), style: null)
                {
                    Color = Color.Black,
                    HoverColor = Color.Black,
                    SelectedColor = Color.Black,
                    PressedColor = Color.Black,
                    CanBeFocused = false,
                };
                new GUIImage(new RectTransform(new Vector2(0.98f), submarinePreviewBackground.RectTransform, Anchor.Center), previewImage, scaleToFit: true) { CanBeFocused = false };
                new GUIFrame(new RectTransform(Vector2.One, submarinePreviewBackground.RectTransform), "InnerGlow", color: Color.Black) { CanBeFocused = false };
            }

            if (SubmarineElement != null)
            {
                new GUIFrame(new RectTransform(Vector2.One * 0.12f, previewButton.RectTransform, anchor: Anchor.BottomRight, pivot: Pivot.BottomRight, scaleBasis: ScaleBasis.BothHeight)
                {
                    AbsoluteOffset = new Point((int)(0.03f * previewButton.Rect.Height))
                },
                    "ExpandButton", Color.White)
                {
                    Color = Color.White,
                    HoverColor = Color.White,
                    PressedColor = Color.White
                };
            }

            var descriptionBox = new GUIListBox(new RectTransform(new Vector2(1, 0.5f), content.RectTransform, Anchor.BottomCenter))
            {
                UserData = "descriptionbox",
                ScrollBarVisible = true,
                Spacing = 5
            };
            
            GUIFont font = parent.Rect.Width < 350 ? GUIStyle.SmallFont : GUIStyle.Font;

            CreateSpecsWindow(descriptionBox, font, includeDescription: true);
        }

        public void CreateSpecsWindow(GUIListBox parent, GUIFont font, bool includeTitle = true, bool includeClass = true, bool includeDescription = false)
        {
            float leftPanelWidth = 0.6f;
            float rightPanelWidth = 0.4f / leftPanelWidth;
            LocalizedString className = !HasTag(SubmarineTag.Shuttle) ? TextManager.Get($"submarineclass.{SubmarineClass}") : TextManager.Get("shuttle");

            int classHeight = (int)GUIStyle.SubHeadingFont.MeasureString(className).Y;
            int leftPanelWidthInt = (int)(parent.Rect.Width * leftPanelWidth);

            GUITextBlock submarineNameText = null;
            GUITextBlock submarineClassText = null;
            if (includeTitle)
            {
                int nameHeight = (int)GUIStyle.LargeFont.MeasureString(DisplayName, true).Y;
                submarineNameText = new GUITextBlock(new RectTransform(new Point(leftPanelWidthInt, nameHeight + HUDLayoutSettings.Padding / 2), parent.Content.RectTransform), DisplayName, textAlignment: Alignment.CenterLeft, font: GUIStyle.LargeFont) { CanBeFocused = false };
                submarineNameText.RectTransform.MinSize = new Point(0, (int)submarineNameText.TextSize.Y);
            }
            if (includeClass)
            {
                submarineClassText = new GUITextBlock(new RectTransform(new Point(leftPanelWidthInt, classHeight), parent.Content.RectTransform), className, textAlignment: Alignment.CenterLeft, font: GUIStyle.SubHeadingFont) { CanBeFocused = false };
                submarineClassText.RectTransform.MinSize = new Point(0, (int)submarineClassText.TextSize.Y);
            }

            if (Price > 0)
            {
                var priceText = new GUITextBlock(new RectTransform(new Vector2(leftPanelWidth, 0), parent.Content.RectTransform),
                    TextManager.Get("subeditor.price"), textAlignment: Alignment.TopLeft, font: font, wrap: true)
                { CanBeFocused = false };
                new GUITextBlock(new RectTransform(new Vector2(rightPanelWidth, 0.0f), priceText.RectTransform, Anchor.TopRight, Pivot.TopLeft),
                    TextManager.GetWithVariable("currencyformat", "[credits]", string.Format(CultureInfo.InvariantCulture, "{0:N0}", Price)), textAlignment: Alignment.TopLeft, font: font, wrap: true)
                { CanBeFocused = false };
            }

            Vector2 realWorldDimensions = Dimensions * Physics.DisplayToRealWorldRatio;
            if (realWorldDimensions != Vector2.Zero)
            {
                LocalizedString dimensionsStr = TextManager.GetWithVariables("DimensionsFormat", ("[width]", ((int)realWorldDimensions.X).ToString()), ("[height]", ((int)realWorldDimensions.Y).ToString()));
                var dimensionsText = new GUITextBlock(new RectTransform(new Vector2(leftPanelWidth, 0), parent.Content.RectTransform),
                    TextManager.Get("Dimensions"), textAlignment: Alignment.TopLeft, font: font, wrap: true)
                { CanBeFocused = false };
                new GUITextBlock(new RectTransform(new Vector2(rightPanelWidth, 0.0f), dimensionsText.RectTransform, Anchor.TopRight, Pivot.TopLeft),
                    dimensionsStr, textAlignment: Alignment.TopLeft, font: font, wrap: true)
                { CanBeFocused = false };
                dimensionsText.RectTransform.MinSize = new Point(0, dimensionsText.Children.First().Rect.Height);
            }

            var cargoCapacityStr = CargoCapacity < 0 ? TextManager.Get("unknown") : TextManager.GetWithVariable("cargocapacityformat", "[cratecount]", CargoCapacity.ToString());
            var cargoCapacityText = new GUITextBlock(new RectTransform(new Vector2(leftPanelWidth, 0), parent.Content.RectTransform),
                TextManager.Get("cargocapacity"), textAlignment: Alignment.TopLeft, font: font, wrap: true)
            { CanBeFocused = false };
            new GUITextBlock(new RectTransform(new Vector2(rightPanelWidth, 0.0f), cargoCapacityText.RectTransform, Anchor.TopRight, Pivot.TopLeft),
                cargoCapacityStr, textAlignment: Alignment.TopLeft, font: font, wrap: true)
            { CanBeFocused = false };
            cargoCapacityText.RectTransform.MinSize = new Point(0, cargoCapacityText.Children.First().Rect.Height);

            if (RecommendedCrewSizeMax > 0)
            {
                var crewSizeText = new GUITextBlock(new RectTransform(new Vector2(leftPanelWidth, 0), parent.Content.RectTransform),
                    TextManager.Get("RecommendedCrewSize"), textAlignment: Alignment.TopLeft, font: font, wrap: true)
                { CanBeFocused = false };
                new GUITextBlock(new RectTransform(new Vector2(rightPanelWidth, 0.0f), crewSizeText.RectTransform, Anchor.TopRight, Pivot.TopLeft),
                    RecommendedCrewSizeMin + " - " + RecommendedCrewSizeMax, textAlignment: Alignment.TopLeft, font: font, wrap: true)
                { CanBeFocused = false };
                crewSizeText.RectTransform.MinSize = new Point(0, crewSizeText.Children.First().Rect.Height);
            }

            if (!string.IsNullOrEmpty(RecommendedCrewExperience))
            {
                var crewExperienceText = new GUITextBlock(new RectTransform(new Vector2(leftPanelWidth, 0), parent.Content.RectTransform),
                    TextManager.Get("RecommendedCrewExperience"), textAlignment: Alignment.TopLeft, font: font, wrap: true)
                { CanBeFocused = false };
                new GUITextBlock(new RectTransform(new Vector2(rightPanelWidth, 0.0f), crewExperienceText.RectTransform, Anchor.TopRight, Pivot.TopLeft),
                    TextManager.Get(RecommendedCrewExperience), textAlignment: Alignment.TopLeft, font: font, wrap: true)
                { CanBeFocused = false };
                crewExperienceText.RectTransform.MinSize = new Point(0, crewExperienceText.Children.First().Rect.Height);
            }

            if (RequiredContentPackages.Any())
            {
                var contentPackagesText = new GUITextBlock(new RectTransform(new Vector2(leftPanelWidth, 0), parent.Content.RectTransform),
                    TextManager.Get("RequiredContentPackages"), textAlignment: Alignment.TopLeft, font: font)
                { CanBeFocused = false };
                new GUITextBlock(new RectTransform(new Vector2(rightPanelWidth, 0.0f), contentPackagesText.RectTransform, Anchor.TopRight, Pivot.TopLeft),
                    string.Join(", ", RequiredContentPackages), textAlignment: Alignment.TopLeft, font: font, wrap: true)
                { CanBeFocused = false };
                contentPackagesText.RectTransform.MinSize = new Point(0, contentPackagesText.Children.First().Rect.Height);
            }

            // show what game version the submarine was created on
            if (!IsVanillaSubmarine() && GameVersion != null)
            {
                var versionText = new GUITextBlock(new RectTransform(new Vector2(leftPanelWidth, 0), parent.Content.RectTransform),
                        TextManager.Get("serverlistversion"), textAlignment: Alignment.TopLeft, font: font, wrap: true)
                { CanBeFocused = false };
                new GUITextBlock(new RectTransform(new Vector2(rightPanelWidth, 0.0f), versionText.RectTransform, Anchor.TopRight, Pivot.TopLeft),
                        GameVersion.ToString(), textAlignment: Alignment.TopLeft, font: font, wrap: true)
                { CanBeFocused = false };

                versionText.RectTransform.MinSize = new Point(0, versionText.Children.First().Rect.Height);
            }

            if (submarineNameText != null)
            {
                submarineNameText.AutoScaleHorizontal = true;
            }

            GUITextBlock descBlock = null;
            if (includeDescription)
            {
                //space
                new GUIFrame(new RectTransform(new Vector2(1.0f, 0.05f), parent.Content.RectTransform), style: null);

                if (!Description.IsNullOrEmpty())
                {
                    var wsItemDesc = new GUITextBlock(new RectTransform(new Vector2(1, 0), parent.Content.RectTransform),
                        TextManager.Get("SaveSubDialogDescription", "WorkshopItemDescription"), font: GUIStyle.Font, wrap: true)
                    { CanBeFocused = false, ForceUpperCase = ForceUpperCase.Yes };

                    descBlock = new GUITextBlock(new RectTransform(new Vector2(1, 0), parent.Content.RectTransform), Description, font: font, wrap: true)
                    {
                        CanBeFocused = false
                    };
                }
            }
            GUITextBlock.AutoScaleAndNormalize(parent.Content.GetAllChildren<GUITextBlock>().Where(c => c != submarineNameText && c != descBlock));
            parent.ForceLayoutRecalculation();
        }
    }
}
