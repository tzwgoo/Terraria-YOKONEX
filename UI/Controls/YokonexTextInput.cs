using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace TerrariaYokonex.UI.Controls
{
    internal sealed class YokonexTextInput : UIElement
    {
        private const float LabelScale = 0.68f;
        private const float ValueScale = 0.7f;
        private const float HintScale = 0.54f;

        private static YokonexTextInput? _focusedInput;
        private string _text;

        public YokonexTextInput(
            string label,
            string initialValue,
            Action<string> onValueChanged,
            string placeholder = "",
            string hint = "",
            bool isPassword = false,
            int maxLength = 256)
        {
            Label = label ?? string.Empty;
            Placeholder = placeholder ?? string.Empty;
            Hint = hint ?? string.Empty;
            IsPassword = isPassword;
            MaxLength = Math.Max(1, maxLength);
            _text = initialValue ?? string.Empty;
            OnValueChanged = onValueChanged;
            Height.Set(string.IsNullOrWhiteSpace(Hint) ? 56f : 74f, 0f);
            Width.Set(0f, 1f);
        }

        public string Label { get; }

        public string Placeholder { get; }

        public string Hint { get; }

        public bool IsPassword { get; }

        public int MaxLength { get; }

        public Action<string> OnValueChanged { get; }

        public string Text
        {
            get => _text;
            set => _text = value ?? string.Empty;
        }

        public bool IsFocused => ReferenceEquals(_focusedInput, this);

        public static bool HasFocusedInput => _focusedInput != null;

        public static void ClearFocus()
        {
            _focusedInput = null;
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            Focus();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Main.LocalPlayer != null)
            {
                Main.LocalPlayer.mouseInterface = true;
            }

            if (IsFocused)
            {
                Main.blockInput = true;

                // 文本输入统一走 Terraria 自带输入法与按键处理，避免中文输入和退格行为不一致。
                string nextText = Main.GetInputText(_text, false);
                if (nextText.Length > MaxLength)
                {
                    nextText = nextText.Substring(0, MaxLength);
                }

                if (!string.Equals(nextText, _text, StringComparison.Ordinal))
                {
                    _text = nextText;
                    OnValueChanged?.Invoke(_text);
                }

                if (Main.inputTextEscape ||
                    IsFreshKeyPress(Keys.Enter) ||
                    IsFreshKeyPress(Keys.Tab))
                {
                    ClearFocus();
                }
            }
            else if (IsFreshKeyPress(Keys.Escape) && IsMouseHovering)
            {
                ClearFocus();
            }
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            CalculatedStyle dimensions = GetDimensions();
            Rectangle backgroundRectangle = new Rectangle(
                (int)dimensions.X,
                (int)(dimensions.Y + 18f),
                (int)dimensions.Width,
                (int)(dimensions.Height - 18f));

            Color borderColor = IsFocused ? new Color(255, 220, 120) : new Color(78, 106, 135);
            Color backgroundColor = IsFocused ? new Color(24, 35, 55, 235) : new Color(18, 27, 41, 215);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, backgroundRectangle, backgroundColor);

            DrawBorder(spriteBatch, backgroundRectangle, borderColor);

            // 输入框标签和提示统一收小一档，避免中文字段名比实际输入内容更抢视觉焦点。
            Utils.DrawBorderString(spriteBatch, Label, new Vector2(dimensions.X, dimensions.Y), new Color(255, 240, 170), LabelScale);

            string displayText = _text;
            if (displayText.Length == 0)
            {
                displayText = Placeholder;
            }
            else if (IsPassword)
            {
                displayText = new string('*', displayText.Length);
            }

            if (IsFocused && Main.GameUpdateCount % 40 < 20)
            {
                displayText += "|";
            }

            Color textColor = _text.Length == 0 ? new Color(160, 170, 180) : Color.White;
            Utils.DrawBorderString(
                spriteBatch,
                displayText,
                new Vector2(dimensions.X + 10f, dimensions.Y + 26f),
                textColor,
                ValueScale);

            if (!string.IsNullOrWhiteSpace(Hint))
            {
                Utils.DrawBorderString(
                    spriteBatch,
                    Hint,
                    new Vector2(dimensions.X + 2f, dimensions.Y + dimensions.Height - 16f),
                    new Color(148, 187, 214),
                    HintScale);
            }
        }

        private void Focus()
        {
            if (ReferenceEquals(_focusedInput, this))
            {
                return;
            }

            _focusedInput = this;
            Main.clrInput();
        }

        private static bool IsFreshKeyPress(Keys key)
        {
            return Main.keyState.IsKeyDown(key) && Main.oldKeyState.IsKeyUp(key);
        }

        private static void DrawBorder(SpriteBatch spriteBatch, Rectangle rectangle, Color color)
        {
            Rectangle top = new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 2);
            Rectangle bottom = new Rectangle(rectangle.X, rectangle.Bottom - 2, rectangle.Width, 2);
            Rectangle left = new Rectangle(rectangle.X, rectangle.Y, 2, rectangle.Height);
            Rectangle right = new Rectangle(rectangle.Right - 2, rectangle.Y, 2, rectangle.Height);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, top, color);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, bottom, color);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, left, color);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, right, color);
        }
    }
}
