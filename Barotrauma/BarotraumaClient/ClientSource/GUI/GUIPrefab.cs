﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Barotrauma
{
    public abstract class GUIPrefab : Prefab
    {
        public GUIPrefab(ContentXElement element, UIStyleFile file) : base(file, element) { }

        protected override Identifier DetermineIdentifier(XElement element)
        {
            return element.NameAsIdentifier();
        }
    }

    public abstract class GUISelector<T> where T : GUIPrefab
    {
        public readonly PrefabSelector<T> Prefabs = new PrefabSelector<T>();
        public readonly Identifier Identifier;

        public GUISelector(string identifier)
        {
            Identifier = identifier.ToIdentifier();
        }
    }

    public class GUIFontPrefab : GUIPrefab
    {
        private readonly ContentXElement element;
        private ScalableFont font;
        public ScalableFont Font
        {
            get
            {
                if (Language != GameSettings.CurrentConfig.Language) { LoadFont(); }
                return font;
            }
        }

        private ScalableFont cjkFont;

        public ScalableFont CjkFont
        {
            get
            {
                if (Language != GameSettings.CurrentConfig.Language) { LoadFont(); }
                if (font.IsCJK) { return font; }
                return cjkFont;
            }
        }

        public LanguageIdentifier Language { get; private set; }

        public GUIFontPrefab(ContentXElement element, UIStyleFile file) : base(element, file)
        {
            this.element = element;
            LoadFont();
        }

        public void LoadFont()
        {
            string fontPath = GetFontFilePath(element);
            uint size = GetFontSize(element);
            bool dynamicLoading = GetFontDynamicLoading(element);
            bool isCJK = GetIsCJK(element);
            font?.Dispose();
            cjkFont?.Dispose();
            font = new ScalableFont(fontPath, size, GameMain.Instance.GraphicsDevice, dynamicLoading, isCJK)
            {
                ForceUpperCase = element.GetAttributeBool("forceuppercase", false)
            };
            if (!isCJK)
            {
                cjkFont = ExtractCjkFont(element, font.Size)
                          ?? new ScalableFont("Content/Fonts/NotoSans/NotoSansCJKsc-Bold.otf",
                                font.Size, GameMain.Instance.GraphicsDevice, dynamicLoading: true, isCJK: true);
                cjkFont.ForceUpperCase = font.ForceUpperCase;
            }
            Language = GameSettings.CurrentConfig.Language;
        }

        public override void Dispose()
        {
            font?.Dispose(); font = null;
            cjkFont?.Dispose(); cjkFont = null;
        }

        private ScalableFont ExtractCjkFont(ContentXElement element, uint size)
        {
            foreach (var subElement in element.Elements().Reverse())
            {
                if (subElement.NameAsIdentifier() != "override") { continue; }

                if (subElement.GetAttributeBool("iscjk", false))
                {
                    return new ScalableFont(subElement, size, GameMain.Instance.GraphicsDevice);
                }
            }
            return null;
        }
        
        private string GetFontFilePath(ContentXElement element)
        {
            foreach (var subElement in element.Elements())
            {
                if (!subElement.Name.ToString().Equals("override", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (GameSettings.CurrentConfig.Language == subElement.GetAttributeIdentifier("language", "").ToLanguageIdentifier())
                {
                    return subElement.GetAttributeContentPath("file")?.Value;
                }
            }
            return element.GetAttributeContentPath("file")?.Value;
        }

        private uint GetFontSize(XElement element, uint defaultSize = 14)
        {
            //check if any of the language override fonts want to override the font size as well
            foreach (var subElement in element.Elements())
            {
                if (!subElement.Name.ToString().Equals("override", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (GameSettings.CurrentConfig.Language == subElement.GetAttributeIdentifier("language", "").ToLanguageIdentifier())
                {
                    uint overrideFontSize = GetFontSize(subElement, 0);
                    if (overrideFontSize > 0) 
                    { 
                        return (uint)Math.Floor(overrideFontSize * GUI.Scale + 0.01);
                    }
                }
            }

            foreach (var subElement in element.Elements())
            {
                if (!subElement.Name.ToString().Equals("size", StringComparison.OrdinalIgnoreCase)) { continue; }
                Point maxResolution = subElement.GetAttributePoint("maxresolution", new Point(int.MaxValue, int.MaxValue));
                if (1920 == maxResolution.X && 1080 == maxResolution.Y)
                {
                    uint size = (uint)subElement.GetAttributeInt("size", 14);
                    if (element.GetAttribute("file").Value.ToString().Equals("Content/Fonts/Oswald-Bold.ttf", StringComparison.OrdinalIgnoreCase))
                    {
                        return size;
                    }
                    else
                    {
                        return (uint)Math.Floor(size * GUI.Scale + 0.01);
                    }
                }
            }
            foreach (var subElement in element.Elements())
            {
                if (!subElement.Name.ToString().Equals("size", StringComparison.OrdinalIgnoreCase)) { continue; }
                Point maxResolution = subElement.GetAttributePoint("maxresolution", new Point(int.MaxValue, int.MaxValue));
                if (int.MaxValue == maxResolution.X && int.MaxValue == maxResolution.Y)
                {
                    return (uint)subElement.GetAttributeInt("size", 14);
                }
            }
            return 0;
        }

        private bool GetFontDynamicLoading(XElement element)
        {
            foreach (var subElement in element.Elements())
            {
                if (!subElement.Name.ToString().Equals("override", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (GameSettings.CurrentConfig.Language == subElement.GetAttributeIdentifier("language", "").ToLanguageIdentifier())
                {
                    return subElement.GetAttributeBool("dynamicloading", false);
                }
            }
            return element.GetAttributeBool("dynamicloading", false);
        }

        private bool GetIsCJK(XElement element)
        {
            foreach (var subElement in element.Elements())
            {
                if (!subElement.Name.ToString().Equals("override", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (GameSettings.CurrentConfig.Language == subElement.GetAttributeIdentifier("language", "").ToLanguageIdentifier())
                {
                    return subElement.GetAttributeBool("iscjk", false);
                }
            }
            return element.GetAttributeBool("iscjk", false);
        }
    }

    public class GUIFont : GUISelector<GUIFontPrefab>
    {
        public GUIFont(string identifier) : base(identifier) { }

        public bool HasValue => Prefabs.Any();
        
        public ScalableFont Value => Prefabs.ActivePrefab.Font;

        public static implicit operator ScalableFont(GUIFont reference) => reference.Value;

        public bool ForceUpperCase => HasValue && Value.ForceUpperCase;

        public uint Size => HasValue ? Value.Size : 0;

        private ScalableFont GetFontForStr(LocalizedString str) => GetFontForStr(str.Value);
        
        private ScalableFont GetFontForStr(string str) =>
            TextManager.IsCJK(str) ? Prefabs.ActivePrefab.CjkFont : Prefabs.ActivePrefab.Font;
        
        public void DrawString(SpriteBatch sb, LocalizedString text, Vector2 position, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects se, float layerDepth)
        {
            DrawString(sb, text.Value, position, color, rotation, origin, scale, se, layerDepth);
        }

        public void DrawString(SpriteBatch sb, string text, Vector2 position, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects se, float layerDepth)
        {
            GetFontForStr(text).DrawString(sb, text, position, color, rotation, origin, scale, se, layerDepth);
        }

        public void DrawString(SpriteBatch sb, LocalizedString text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects se, float layerDepth, Alignment alignment = Alignment.TopLeft)
        {
            DrawString(sb, text.Value, position, color, rotation, origin, scale, se, layerDepth, alignment);
        }

        public void DrawString(SpriteBatch sb, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects se, float layerDepth, Alignment alignment = Alignment.TopLeft, ForceUpperCase forceUpperCase = Barotrauma.ForceUpperCase.Inherit)
        {
            GetFontForStr(text).DrawString(sb, text, position, color, rotation, origin, scale, se, layerDepth, alignment, forceUpperCase);
        }

        public void DrawString(SpriteBatch sb, LocalizedString text, Vector2 position, Color color, ForceUpperCase forceUpperCase = Barotrauma.ForceUpperCase.Inherit, bool italics = false)
        {
            DrawString(sb, text.Value, position, color, forceUpperCase, italics);
        }

        public void DrawString(SpriteBatch sb, string text, Vector2 position, Color color, ForceUpperCase forceUpperCase = Barotrauma.ForceUpperCase.Inherit, bool italics = false)
        {
            GetFontForStr(text).DrawString(sb, text, position, color, forceUpperCase, italics);
        }

        public void DrawStringWithColors(SpriteBatch sb, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects se, float layerDepth, in ImmutableArray<RichTextData>? richTextData, int rtdOffset = 0, Alignment alignment = Alignment.TopLeft, ForceUpperCase forceUpperCase = Barotrauma.ForceUpperCase.Inherit)
        {
            GetFontForStr(text).DrawStringWithColors(sb, text, position, color, rotation, origin, scale, se, layerDepth, richTextData, rtdOffset, alignment, forceUpperCase);
        }

        public Vector2 MeasureString(LocalizedString str, bool removeExtraSpacing = false)
        {
            return GetFontForStr(str).MeasureString(str, removeExtraSpacing);
        }

        public Vector2 MeasureChar(char c)
        {
            return GetFontForStr($"{c}").MeasureChar(c);
        }

        public string WrapText(string text, float width)
            => GetFontForStr(text).WrapText(text, width);
        
        public string WrapText(string text, float width, int requestCharPos, out Vector2 requestedCharPos)
            => GetFontForStr(text).WrapText(text, width, requestCharPos, out requestedCharPos);
        
        public string WrapText(string text, float width, out Vector2[] allCharPositions)
            => GetFontForStr(text).WrapText(text, width, out allCharPositions);

        public float LineHeight => Value.LineHeight;
    }

    public class GUIColorPrefab : GUIPrefab
    {
        public readonly Color Color;

        public GUIColorPrefab(ContentXElement element, UIStyleFile file) : base(element, file)
        {
            Color = element.GetAttributeColor("color", Color.White);
        }

        public override void Dispose() { }
    }

    public class GUIColor : GUISelector<GUIColorPrefab>
    {
        public GUIColor(string identifier) : base(identifier) { }

        public Color Value
        {
            get
            {
                return Prefabs.ActivePrefab.Color;
            }
        }

        public static implicit operator Color(GUIColor reference) => reference.Value;

        public static Color operator*(GUIColor value, float scale)
        {
            return value.Value * scale;
        }
    }

    public class GUISpritePrefab : GUIPrefab
    {
        public readonly UISprite Sprite;

        public GUISpritePrefab(ContentXElement element, UIStyleFile file) : base(element, file)
        {
            Sprite = new UISprite(element);
        }

        public override void Dispose()
        {
            Sprite.Sprite.Remove();
        }
    }

    public class GUISprite : GUISelector<GUISpritePrefab>
    {
        public GUISprite(string identifier) : base(identifier) { }

        public UISprite Value
        {
            get
            {
                return Prefabs.ActivePrefab.Sprite;
            }
        }

        public static implicit operator UISprite(GUISprite reference) => reference.Value;

        public void Draw(SpriteBatch spriteBatch, Rectangle rect, Color color, SpriteEffects spriteEffects = SpriteEffects.None)
        {
            Value.Draw(spriteBatch, rect, color, spriteEffects);
        }
    }

    public class GUISpriteSheetPrefab : GUIPrefab
    {
        public readonly SpriteSheet SpriteSheet;
        
        public GUISpriteSheetPrefab(ContentXElement element, UIStyleFile file) : base(element, file)
        {
            SpriteSheet = new SpriteSheet(element);
        }

        public override void Dispose()
        {
            SpriteSheet.Remove();
        }
    }

    public class GUISpriteSheet : GUISelector<GUISpriteSheetPrefab>
    {
        public GUISpriteSheet(string identifier) : base(identifier) { }

        public SpriteSheet Value
        {
            get
            {
                return Prefabs.ActivePrefab.SpriteSheet;
            }
        }

        public int FrameCount => Value.FrameCount;
        public Point FrameSize => Value.FrameSize;

        public void Draw(ISpriteBatch spriteBatch, Vector2 pos, float rotate = 0, float scale = 1, SpriteEffects spriteEffects = SpriteEffects.None)
        {
            Value.Draw(spriteBatch, pos, rotate, scale, spriteEffects);
        }

        public void Draw(ISpriteBatch spriteBatch, Vector2 pos, Color color, Vector2 origin, float rotate = 0, float scale = 1, SpriteEffects spriteEffects = SpriteEffects.None, float? depth = null)
        {
            Value.Draw(spriteBatch, pos, color, origin, rotate, scale, spriteEffects, depth);
        }

        public void Draw(ISpriteBatch spriteBatch, int spriteIndex, Vector2 pos, Color color, Vector2 origin, float rotate, Vector2 scale, SpriteEffects spriteEffects = SpriteEffects.None, float? depth = null)
        {
            Value.Draw(spriteBatch, spriteIndex, pos, color, origin, rotate, scale, spriteEffects, depth);
        }

        public static implicit operator SpriteSheet(GUISpriteSheet reference) => reference.Value;
    }

    public class GUICursorPrefab : GUIPrefab
    {
        public readonly Sprite[] Sprites;

        public GUICursorPrefab(ContentXElement element, UIStyleFile file) : base(element, file)
        {
            Sprites = new Sprite[Enum.GetValues(typeof(CursorState)).Length];
            foreach (var subElement in element.Elements())
            {
                CursorState state = subElement.GetAttributeEnum("state", CursorState.Default);
                Sprites[(int)state] = new Sprite(subElement);
            }
        }

        public override void Dispose()
        {
            foreach (var sprite in Sprites)
            {
                sprite?.Remove();
            }
        }
    }

    public class GUICursor : GUISelector<GUICursorPrefab>
    {
        public GUICursor(string identifier) : base(identifier) { }

        public Sprite this[CursorState k] => Prefabs.ActivePrefab.Sprites[(int)k];
    }
}
