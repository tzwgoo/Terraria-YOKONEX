using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using TerrariaYokonex.Core.Config;
using TerrariaYokonex.Core.Models;
using TerrariaYokonex.Core.Services;
using TerrariaYokonex.Systems;
using TerrariaYokonex.UI.Controls;

namespace TerrariaYokonex.UI
{
    internal enum YokonexConfigTab
    {
        Connection,
        Rules,
    }

    internal sealed class TerrariaYokonexConfigUiState : UIState
    {
        // 统一字号层级，避免中文标题、按钮和字段标签各用一套比例导致视觉失衡。
        private const float WindowTitleScale = 0.86f;
        private const float WindowSubtitleScale = 0.62f;
        private const float PanelTitleScale = 0.76f;
        private const float CardTitleScale = 0.68f;
        private const float BodyTextScale = 0.58f;
        private const float StatusTextScale = 0.6f;
        private const float ButtonTextScale = 0.66f;
        private const float RuleListTitleScale = 0.56f;
        private const float RuleListMetaScale = 0.46f;
        private const float RuleDetailTitleScale = 0.6f;
        private const float RuleDetailBodyScale = 0.5f;

        private readonly List<Func<string?>> _validators = new List<Func<string?>>();
        private YokonexConfigSnapshot _snapshot;
        private YokonexConfigTab _activeTab;
        private int _selectedRuleIndex;
        private string _statusMessage;
        private UIText? _statusLabel;
        private YokonexTextInput? _testEventKeyInput;
        private YokonexTextInput? _testMatchValueInput;

        public TerrariaYokonexConfigUiState(YokonexConfigSnapshot snapshot)
        {
            _snapshot = snapshot ?? new YokonexConfigSnapshot();
            _activeTab = YokonexConfigTab.Connection;
            _selectedRuleIndex = 0;
            _statusMessage = "当前仅保留 IM 链路。";
            EnsureValidRuleSelection();
        }

        public override void OnInitialize()
        {
            RebuildInterface();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Main.LocalPlayer != null)
            {
                Main.LocalPlayer.mouseInterface = true;
            }

            if (Main.gameMenu)
            {
                TerrariaYokonexUiSystem.Instance?.CloseConfigUi();
                return;
            }

            if (!YokonexTextInput.HasFocusedInput &&
                (IsFreshKeyPress(Keys.Escape) || TerrariaYokonex.OpenConfigHotkey?.JustPressed == true))
            {
                TerrariaYokonexUiSystem.Instance?.CloseConfigUi();
            }
        }

        private void RebuildInterface()
        {
            EnsureValidRuleSelection();
            YokonexTextInput.ClearFocus();
            _validators.Clear();
            // 每次切页或保存后整体重建，保证界面展示和当前配置快照完全一致。
            Elements.Clear();

            UIPanel overlay = new UIPanel();
            overlay.Left.Set(0f, 0f);
            overlay.Top.Set(0f, 0f);
            overlay.Width.Set(0f, 1f);
            overlay.Height.Set(0f, 1f);
            overlay.BackgroundColor = new Color(7, 12, 22, 210);
            overlay.BorderColor = new Color(7, 12, 22, 0);
            Append(overlay);

            UIPanel window = new UIPanel();
            window.Left.Set(60f, 0f);
            window.Top.Set(40f, 0f);
            window.Width.Set(-120f, 1f);
            window.Height.Set(-80f, 1f);
            window.BackgroundColor = new Color(18, 29, 45, 242);
            window.BorderColor = new Color(74, 105, 138);
            overlay.Append(window);

            UIText title = new UIText("Terraria YOKONEX IM", WindowTitleScale, true);
            title.Left.Set(20f, 0f);
            title.Top.Set(16f, 0f);
            window.Append(title);

            UIText subtitle = new UIText("配置中心 · ESC 关闭，回车或 Tab 结束输入。", WindowSubtitleScale);
            subtitle.Left.Set(22f, 0f);
            subtitle.Top.Set(52f, 0f);
            window.Append(subtitle);

            _statusLabel = new UIText(string.Empty, StatusTextScale);
            _statusLabel.Left.Set(22f, 0f);
            _statusLabel.Top.Set(72f, 0f);
            window.Append(_statusLabel);
            SetStatus(_statusMessage);

            window.Append(CreateTopButton("保存并应用", 0f, 20f, 138f, SaveSnapshotFromUi));
            window.Append(CreateTopButton("重载磁盘配置", 148f, 20f, 138f, ReloadSnapshotFromDisk));
            window.Append(CreateTopButton("关闭", 296f, 20f, 82f, () => TerrariaYokonexUiSystem.Instance?.CloseConfigUi()));

            window.Append(CreateTabButton("连接设置", YokonexConfigTab.Connection, 24f));
            window.Append(CreateTabButton("规则配置", YokonexConfigTab.Rules, 142f));

            UIPanel body = new UIPanel();
            body.Left.Set(20f, 0f);
            body.Top.Set(124f, 0f);
            body.Width.Set(-40f, 1f);
            body.Height.Set(-144f, 1f);
            body.BackgroundColor = new Color(11, 18, 29, 190);
            body.BorderColor = new Color(54, 78, 110);
            window.Append(body);

            switch (_activeTab)
            {
                case YokonexConfigTab.Connection:
                    BuildSettingsTab(body);
                    break;
                case YokonexConfigTab.Rules:
                    BuildRulesTab(body);
                    break;
            }
        }

        private void BuildSettingsTab(UIPanel body)
        {
            UIPanel rightPanel = new UIPanel();
            rightPanel.Left.Set(12f, 0f);
            rightPanel.Top.Set(12f, 0f);
            rightPanel.Width.Set(-24f, 1f);
            rightPanel.Height.Set(-24f, 1f);
            rightPanel.BackgroundColor = new Color(18, 30, 45, 220);
            rightPanel.BorderColor = new Color(60, 86, 118);
            body.Append(rightPanel);

            UIText rightTitle = new UIText("详细参数", PanelTitleScale, true);
            rightTitle.Left.Set(12f, 0f);
            rightTitle.Top.Set(12f, 0f);
            rightPanel.Append(rightTitle);

            UIList list = CreateScrollableList(rightPanel, 12f, 42f, -24f, -54f);

            list.Add(CreateSectionPanel("运行总控", "控制总开关与基础行为。"));
            list.Add(CreateToggleField(
                "启用 YOKONEX",
                "关闭后不再发送 IM 指令。",
                () => _snapshot.Settings.Enabled,
                value => _snapshot.Settings.Enabled = value));
            list.Add(CreateToggleField(
                "调试日志",
                "联调时开启，稳定后可关闭。",
                () => _snapshot.Settings.DebugLogging,
                value => _snapshot.Settings.DebugLogging = value));
            list.Add(CreateIntegerField(
                "全局冷却时间 (ms)",
                _snapshot.Settings.GlobalCooldownMs,
                value => _snapshot.Settings.GlobalCooldownMs = value,
                "所有事件共享冷却。"));

            list.Add(CreateSectionPanel("IM / WebSocket 输出", "配置 IM 连接参数。"));
            list.Add(CreateToggleField(
                "启用 WebSocket 输出",
                "命中规则后发送 IM 指令。",
                () => _snapshot.Settings.WebSocket.Enabled,
                value => _snapshot.Settings.WebSocket.Enabled = value));
            list.Add(CreateActionRow(
                "连接操作",
                "手动登录或退出 IM。",
                new[]
                {
                    CreateActionButtonSpec("登录 IM", LoginImSession, 110f),
                    CreateActionButtonSpec("退出登录", LogoutImSession, 110f),
                    CreateActionButtonSpec("默认 WS", () =>
                    {
                        _snapshot.Settings.WebSocket.WsUrl = YokonexKnownValues.DefaultWebSocketUrl;
                        SetStatus("已填入默认 WebSocket 地址，记得保存并应用。");
                        RebuildInterface();
                    }, 110f),
                }));
            list.Add(CreateTextField(
                "WebSocket 地址",
                _snapshot.Settings.WebSocket.WsUrl,
                value => _snapshot.Settings.WebSocket.WsUrl = value.Trim(),
                YokonexKnownValues.DefaultWebSocketUrl,
                "IM 服务地址。"));
            list.Add(CreateTextField(
                "UserId",
                _snapshot.Settings.WebSocket.UserId,
                value => _snapshot.Settings.WebSocket.UserId = value.Trim(),
                "123456",
                "推荐填纯数字 ID。"));
            list.Add(CreateTextField(
                "Token",
                _snapshot.Settings.WebSocket.Token,
                value => _snapshot.Settings.WebSocket.Token = value.Trim(),
                "请输入 Token",
                "按密码样式显示。",
                true));
            list.Add(CreateIntegerField(
                "连接超时 (ms)",
                _snapshot.Settings.WebSocket.ConnectTimeoutMs,
                value => _snapshot.Settings.WebSocket.ConnectTimeoutMs = value,
                "连接超时。"));
            list.Add(CreateIntegerField(
                "接收超时 (ms)",
                _snapshot.Settings.WebSocket.ReceiveTimeoutMs,
                value => _snapshot.Settings.WebSocket.ReceiveTimeoutMs = value,
                "响应超时。"));
        }

        private void BuildRulesTab(UIPanel body)
        {
            UIPanel leftPanel = new UIPanel();
            leftPanel.Left.Set(12f, 0f);
            leftPanel.Top.Set(12f, 0f);
            leftPanel.Width.Set(300f, 0f);
            leftPanel.Height.Set(-24f, 1f);
            leftPanel.BackgroundColor = new Color(18, 30, 45, 220);
            leftPanel.BorderColor = new Color(60, 86, 118);
            body.Append(leftPanel);

            UIText leftTitle = new UIText("事件列表", PanelTitleScale, true);
            leftTitle.Left.Set(12f, 0f);
            leftTitle.Top.Set(12f, 0f);
            leftPanel.Append(leftTitle);

            UIList ruleList = CreateScrollableList(leftPanel, 12f, 42f, -24f, -54f);
            for (int index = 0; index < _snapshot.Routes.Rules.Count; index++)
            {
                ruleList.Add(CreateRuleListItem(index, _snapshot.Routes.Rules[index]));
            }

            UIPanel rightPanel = new UIPanel();
            rightPanel.Left.Set(324f, 0f);
            rightPanel.Top.Set(12f, 0f);
            rightPanel.Width.Set(-336f, 1f);
            rightPanel.Height.Set(-24f, 1f);
            rightPanel.BackgroundColor = new Color(18, 30, 45, 220);
            rightPanel.BorderColor = new Color(60, 86, 118);
            body.Append(rightPanel);

            UIText rightTitle = new UIText("事件配置", PanelTitleScale, true);
            rightTitle.Left.Set(12f, 0f);
            rightTitle.Top.Set(12f, 0f);
            rightPanel.Append(rightTitle);

            if (_snapshot.Routes.Rules.Count == 0)
            {
                UIText emptyText = new UIText("当前没有可配置事件。", CardTitleScale);
                emptyText.Left.Set(14f, 0f);
                emptyText.Top.Set(52f, 0f);
                rightPanel.Append(emptyText);
                return;
            }

            YokonexRouteRule selectedRule = _snapshot.Routes.Rules[_selectedRuleIndex];
            UIList editorList = CreateScrollableList(rightPanel, 12f, 42f, -24f, -54f);
            editorList.Add(CreateInfoCard(
                "事件名称",
                YokonexKnownValues.GetEventDisplayName(selectedRule.EventKey),
                new Color(124, 193, 255),
                80f,
                RuleDetailTitleScale,
                RuleDetailBodyScale));
            editorList.Add(CreateToggleField(
                "启用状态",
                "关闭后不触发。",
                () => selectedRule.Enabled,
                value => selectedRule.Enabled = value));
            editorList.Add(CreateInfoCard(
                "Command ID",
                selectedRule.CommandId,
                new Color(136, 224, 172),
                80f,
                RuleDetailTitleScale,
                RuleDetailBodyScale));
        }

        private void BuildToolsTab(UIPanel body)
        {
            UIPanel leftPanel = new UIPanel();
            leftPanel.Left.Set(12f, 0f);
            leftPanel.Top.Set(12f, 0f);
            leftPanel.Width.Set(420f, 0f);
            leftPanel.Height.Set(-24f, 1f);
            leftPanel.BackgroundColor = new Color(18, 30, 45, 220);
            leftPanel.BorderColor = new Color(60, 86, 118);
            body.Append(leftPanel);

            UIText toolTitle = new UIText("测试与状态", 0.86f, true);
            toolTitle.Left.Set(12f, 0f);
            toolTitle.Top.Set(10f, 0f);
            leftPanel.Append(toolTitle);

            _testEventKeyInput = CreateTextField(
                "测试事件键",
                _testEventKeyInput?.Text ?? "player_hurt",
                _ => { },
                "player_hurt",
                "用于发送测试事件。");
            _testEventKeyInput.Left.Set(12f, 0f);
            _testEventKeyInput.Top.Set(48f, 0f);
            _testEventKeyInput.Width.Set(-24f, 1f);
            leftPanel.Append(_testEventKeyInput);

            _testMatchValueInput = CreateTextField(
                "测试匹配值",
                _testMatchValueInput?.Text ?? string.Empty,
                _ => { },
                "可留空",
                "留空即可。");
            _testMatchValueInput.Left.Set(12f, 0f);
            _testMatchValueInput.Top.Set(128f, 0f);
            _testMatchValueInput.Width.Set(-24f, 1f);
            leftPanel.Append(_testMatchValueInput);

            leftPanel.Append(CreatePanelButton("发送测试事件", 12f, 214f, 120f, TriggerTestEvent));
            leftPanel.Append(CreatePanelButton("重读运行时状态", 144f, 214f, 120f, ReloadSnapshotFromDisk));

            UIText statusText = new UIText(BuildToolSummaryText(), 0.72f);
            statusText.Left.Set(12f, 0f);
            statusText.Top.Set(266f, 0f);
            leftPanel.Append(statusText);

            UIPanel rightPanel = new UIPanel();
            rightPanel.Left.Set(444f, 0f);
            rightPanel.Top.Set(12f, 0f);
            rightPanel.Width.Set(-456f, 1f);
            rightPanel.Height.Set(-24f, 1f);
            rightPanel.BackgroundColor = new Color(18, 30, 45, 220);
            rightPanel.BorderColor = new Color(60, 86, 118);
            body.Append(rightPanel);

            UIText guideTitle = new UIText("联调提示", 0.86f, true);
            guideTitle.Left.Set(12f, 0f);
            guideTitle.Top.Set(10f, 0f);
            rightPanel.Append(guideTitle);

            UIList guideList = CreateScrollableList(rightPanel, 12f, 46f, -24f, -58f);
            guideList.Add(CreateInfoCard(
                "推荐顺序",
                "1. 开启 WebSocket。\n2. 填好 wsUrl / uid / token / userId。\n3. 配置 commandId。\n4. 发送测试事件。",
                new Color(136, 224, 172),
                138f));
            guideList.Add(CreateInfoCard(
                "迁移提醒",
                "旧版蓝牙规则会自动停用。",
                new Color(246, 200, 118),
                90f));
            guideList.Add(CreateSectionPanel(
                "支持事件清单",
                BuildWrappedLines(YokonexKnownValues.SupportedEventKeys, 2)));
        }

        private UITextPanel<string> CreateTopButton(string text, float leftOffset, float top, float width, Action onClick)
        {
            UITextPanel<string> button = CreateButton(text, onClick, width);
            button.Left.Set(-(width + 24f + leftOffset), 1f);
            button.Top.Set(top, 0f);
            return button;
        }

        private UITextPanel<string> CreateTabButton(string text, YokonexConfigTab tab, float left)
        {
            UITextPanel<string> button = CreateButton(text, () =>
            {
                _activeTab = tab;
                RebuildInterface();
            }, 110f);
            button.Left.Set(left, 0f);
            button.Top.Set(92f, 0f);
            if (_activeTab == tab)
            {
                button.BackgroundColor = new Color(82, 128, 181);
            }

            return button;
        }

        private UITextPanel<string> CreatePanelButton(string text, float left, float top, float width, Action onClick)
        {
            UITextPanel<string> button = CreateButton(text, onClick, width);
            button.Left.Set(left, 0f);
            button.Top.Set(top, 0f);
            return button;
        }

        private UITextPanel<string> CreateButton(string text, Action onClick, float width)
        {
            UITextPanel<string> button = new UITextPanel<string>(text, ButtonTextScale, false);
            button.Width.Set(width, 0f);
            button.Height.Set(30f, 0f);
            button.BackgroundColor = new Color(54, 86, 122);
            button.BorderColor = new Color(110, 146, 185);
            button.OnLeftClick += (_, _) => onClick();
            button.OnMouseOver += (_, _) => button.BackgroundColor = new Color(74, 112, 155);
            button.OnMouseOut += (_, _) =>
            {
                if (button.BackgroundColor != new Color(82, 128, 181))
                {
                    button.BackgroundColor = new Color(54, 86, 122);
                }
            };
            return button;
        }

        private UIList CreateScrollableList(UIElement parent, float left, float top, float widthOffset, float heightOffset)
        {
            UIElement host = new UIElement();
            host.Left.Set(left, 0f);
            host.Top.Set(top, 0f);
            host.Width.Set(widthOffset, 1f);
            host.Height.Set(heightOffset, 1f);
            parent.Append(host);

            UIList list = new UIList();
            list.Left.Set(0f, 0f);
            list.Top.Set(0f, 0f);
            list.Width.Set(-28f, 1f);
            list.Height.Set(0f, 1f);
            list.ListPadding = 8f;
            list.ManualSortMethod = _ => { };
            host.Append(list);

            UIScrollbar scrollbar = new UIScrollbar();
            scrollbar.Left.Set(-20f, 1f);
            scrollbar.Top.Set(0f, 0f);
            scrollbar.Height.Set(0f, 1f);
            host.Append(scrollbar);
            list.SetScrollbar(scrollbar);

            return list;
        }

        private UIElement CreateSectionPanel(string title, string description)
        {
            UIPanel panel = new UIPanel();
            panel.Width.Set(0f, 1f);
            panel.Height.Set(72f, 0f);
            panel.BackgroundColor = new Color(26, 41, 60);
            panel.BorderColor = new Color(70, 100, 132);

            UIText titleText = new UIText(title, CardTitleScale, true);
            titleText.Left.Set(12f, 0f);
            titleText.Top.Set(10f, 0f);
            panel.Append(titleText);

            UIText descriptionText = new UIText(description, BodyTextScale);
            descriptionText.Left.Set(12f, 0f);
            descriptionText.Top.Set(36f, 0f);
            panel.Append(descriptionText);
            return panel;
        }

        private UIElement CreateInfoCard(
            string title,
            string body,
            Color accentColor,
            float height,
            float titleScale = CardTitleScale,
            float bodyScale = BodyTextScale)
        {
            UIPanel panel = new UIPanel();
            panel.Width.Set(0f, 1f);
            panel.Height.Set(height, 0f);
            panel.BackgroundColor = new Color(19, 31, 48);
            panel.BorderColor = new Color(57, 81, 111);

            UIPanel accent = new UIPanel();
            accent.Left.Set(0f, 0f);
            accent.Top.Set(0f, 0f);
            accent.Width.Set(6f, 0f);
            accent.Height.Set(0f, 1f);
            accent.BackgroundColor = accentColor;
            accent.BorderColor = accentColor;
            panel.Append(accent);

            UIText titleText = new UIText(title, titleScale, true);
            titleText.Left.Set(18f, 0f);
            titleText.Top.Set(10f, 0f);
            titleText.TextColor = accentColor;
            panel.Append(titleText);

            UIText bodyText = new UIText(body, bodyScale);
            bodyText.Left.Set(18f, 0f);
            bodyText.Top.Set(36f, 0f);
            panel.Append(bodyText);
            return panel;
        }

        private UIElement CreateActionRow(string title, string description, IReadOnlyList<ActionButtonSpec> buttons)
        {
            UIPanel panel = new UIPanel();
            panel.Width.Set(0f, 1f);
            panel.Height.Set(94f, 0f);
            panel.BackgroundColor = new Color(19, 31, 48);
            panel.BorderColor = new Color(57, 81, 111);

            UIText titleText = new UIText(title, CardTitleScale, true);
            titleText.Left.Set(12f, 0f);
            titleText.Top.Set(10f, 0f);
            panel.Append(titleText);

            UIText descriptionText = new UIText(description, BodyTextScale);
            descriptionText.Left.Set(12f, 0f);
            descriptionText.Top.Set(32f, 0f);
            panel.Append(descriptionText);

            float left = 12f;
            float top = 52f;
            foreach (ActionButtonSpec buttonSpec in buttons)
            {
                UITextPanel<string> button = CreateButton(buttonSpec.Text, buttonSpec.OnClick, buttonSpec.Width);
                button.Left.Set(left, 0f);
                button.Top.Set(top, 0f);
                panel.Append(button);

                left += buttonSpec.Width + 8f;
                if (left > 540f)
                {
                    left = 12f;
                    top += 34f;
                }
            }

            return panel;
        }

        private UIElement CreateToggleField(string title, string description, Func<bool> getter, Action<bool> setter)
        {
            UIPanel panel = new UIPanel();
            panel.Width.Set(0f, 1f);
            panel.Height.Set(70f, 0f);
            panel.BackgroundColor = new Color(19, 31, 48);
            panel.BorderColor = new Color(57, 81, 111);

            UIText titleText = new UIText(title, CardTitleScale, true);
            titleText.Left.Set(12f, 0f);
            titleText.Top.Set(10f, 0f);
            panel.Append(titleText);

            UIText descriptionText = new UIText(description, BodyTextScale);
            descriptionText.Left.Set(12f, 0f);
            descriptionText.Top.Set(36f, 0f);
            panel.Append(descriptionText);

            UITextPanel<string>? button = null;
            button = CreateButton(getter() ? "已启用" : "已关闭", () =>
            {
                bool nextValue = !getter();
                setter(nextValue);
                button?.SetText(nextValue ? "已启用" : "已关闭");
                SetStatus(title + " 已修改，记得保存并应用。");
            }, 100f);
            button.Left.Set(-112f, 1f);
            button.Top.Set(18f, 0f);
            panel.Append(button);
            return panel;
        }

        private YokonexTextInput CreateTextField(
            string label,
            string value,
            Action<string> onChanged,
            string placeholder,
            string hint,
            bool isPassword = false)
        {
            YokonexTextInput input = new YokonexTextInput(label, value, nextValue =>
            {
                onChanged(nextValue);
            }, placeholder, hint, isPassword);
            input.Width.Set(0f, 1f);
            return input;
        }

        private UIElement CreateIntegerField(string label, int value, Action<int> onChanged, string hint)
        {
            YokonexTextInput input = new YokonexTextInput(
                label,
                value.ToString(CultureInfo.InvariantCulture),
                nextValue =>
                {
                    if (TryParseNonNegativeInt(nextValue, out int parsedValue))
                    {
                        onChanged(parsedValue);
                    }
                    else if (!string.IsNullOrWhiteSpace(nextValue))
                    {
                        SetStatus(label + " 需要填写非负整数，保存前会再次校验。");
                    }
                },
                "0",
                hint,
                false,
                16);
            input.Width.Set(0f, 1f);

            _validators.Add(() =>
            {
                if (!TryParseNonNegativeInt(input.Text, out int _))
                {
                    return label + " 需要填写非负整数。";
                }

                return null;
            });
            return input;
        }

        private UIElement CreateRuleListItem(int index, YokonexRouteRule rule)
        {
            // 规则页左侧需要同时展示中文事件名和 command_id，这里单独压小字号，避免列表阅读过于拥挤。
            UIPanel panel = new UIPanel();
            panel.Width.Set(0f, 1f);
            panel.Height.Set(84f, 0f);
            panel.BackgroundColor = index == _selectedRuleIndex ? new Color(57, 89, 128) : new Color(19, 31, 48);
            panel.BorderColor = new Color(66, 97, 132);

            UIText idText = new UIText(TrimText(YokonexKnownValues.GetEventDisplayName(rule.EventKey), 18), RuleListTitleScale, true);
            idText.Left.Set(10f, 0f);
            idText.Top.Set(10f, 0f);
            panel.Append(idText);

            UIText routeText = new UIText("command_id: " + TrimText(rule.CommandId, 22), RuleListMetaScale);
            routeText.Left.Set(10f, 0f);
            routeText.Top.Set(38f, 0f);
            panel.Append(routeText);

            UIText statusText = new UIText(rule.Enabled ? "状态: 已启用" : "状态: 已关闭", RuleListMetaScale);
            statusText.Left.Set(10f, 0f);
            statusText.Top.Set(58f, 0f);
            panel.Append(statusText);

            panel.OnLeftClick += (_, _) =>
            {
                _selectedRuleIndex = index;
                RebuildInterface();
            };
            return panel;
        }

        private void AddRule()
        {
            int nextIndex = _snapshot.Routes.Rules.Count + 1;
            _snapshot.Routes.Rules.Add(new YokonexRouteRule
            {
                Id = "rule-" + nextIndex,
                Notes = "新 IM 规则",
                Enabled = true,
                EventKey = "player_hurt",
                MatchValue = string.Empty,
                OutputMode = YokonexOutputModes.WebSocketCommand,
                CommandId = "player_hurt",
                MinIntervalMs = 1000,
            });
            _selectedRuleIndex = _snapshot.Routes.Rules.Count - 1;
            SetStatus("已新增规则，记得保存并应用。");
            RebuildInterface();
        }

        private void DuplicateSelectedRule()
        {
            if (_snapshot.Routes.Rules.Count == 0)
            {
                SetStatus("当前没有可复制的规则。");
                return;
            }

            YokonexRouteRule source = _snapshot.Routes.Rules[_selectedRuleIndex];
            _snapshot.Routes.Rules.Insert(_selectedRuleIndex + 1, new YokonexRouteRule
            {
                Id = source.Id + "-copy",
                Notes = source.Notes,
                Enabled = source.Enabled,
                EventKey = source.EventKey,
                MatchValue = source.MatchValue,
                OutputMode = YokonexOutputModes.WebSocketCommand,
                CommandId = source.CommandId,
                MinIntervalMs = source.MinIntervalMs,
            });
            _selectedRuleIndex++;
            SetStatus("已复制当前规则，建议手动调整规则 ID。");
            RebuildInterface();
        }

        private void DeleteSelectedRule()
        {
            if (_snapshot.Routes.Rules.Count == 0)
            {
                SetStatus("当前没有可删除的规则。");
                return;
            }

            string deletedRuleId = _snapshot.Routes.Rules[_selectedRuleIndex].Id;
            _snapshot.Routes.Rules.RemoveAt(_selectedRuleIndex);
            EnsureValidRuleSelection();
            SetStatus("已删除规则: " + deletedRuleId);
            RebuildInterface();
        }

        private void SaveSnapshotFromUi()
        {
            if (!ValidateBeforeSave(out string validationError))
            {
                SetStatus("保存失败: " + validationError);
                return;
            }

            TerrariaYokonexRuntime? runtime = TerrariaYokonexRuntime.Instance;
            if (runtime == null)
            {
                SetStatus("保存失败: 运行时尚未初始化。");
                return;
            }

            try
            {
                // 保存前统一锁定为 IM 输出模式，避免旧配置残留值再次写回磁盘。
                if (!string.IsNullOrWhiteSpace(_snapshot.Settings.WebSocket.UserId))
                {
                    // UID 已从界面隐藏，保存时优先根据 UserId 自动派生，避免旧 UID 残留造成登录错位。
                    _snapshot.Settings.WebSocket.Uid = string.Empty;
                }

                foreach (YokonexRouteRule rule in _snapshot.Routes.Rules)
                {
                    rule.OutputMode = YokonexOutputModes.WebSocketCommand;
                }

                runtime.SaveSnapshot(_snapshot);
                _snapshot = runtime.GetEditableSnapshot();
                SetStatus("配置已保存并立即应用到当前运行时。");
                RebuildInterface();
            }
            catch (Exception ex)
            {
                SetStatus("保存失败: " + ex.Message);
            }
        }

        private void ReloadSnapshotFromDisk()
        {
            TerrariaYokonexRuntime? runtime = TerrariaYokonexRuntime.Instance;
            if (runtime == null)
            {
                SetStatus("重载失败: 运行时尚未初始化。");
                return;
            }

            runtime.ReloadConfig();
            _snapshot = runtime.GetEditableSnapshot();
            EnsureValidRuleSelection();
            SetStatus("已从磁盘重新加载配置，未保存的界面修改已丢弃。");
            RebuildInterface();
        }

        private void LoginImSession()
        {
            TerrariaYokonexRuntime? runtime = TerrariaYokonexRuntime.Instance;
            if (runtime == null)
            {
                SetStatus("登录失败: 运行时尚未初始化。");
                return;
            }

            if (!string.IsNullOrWhiteSpace(_snapshot.Settings.WebSocket.UserId))
            {
                _snapshot.Settings.WebSocket.Uid = string.Empty;
            }

            if (!ValidateBeforeSave(out string validationError))
            {
                SetStatus("登录失败: " + validationError);
                return;
            }

            try
            {
                // 登录按钮直接使用当前界面里的连接参数，避免仍然拿旧运行时配置去建链。
                runtime.SaveSnapshot(_snapshot);
                _snapshot = runtime.GetEditableSnapshot();
            }
            catch (Exception ex)
            {
                SetStatus("登录失败: " + ex.Message);
                return;
            }

            YokonexDispatchResult result = runtime.LoginWebSocketAsync(default).GetAwaiter().GetResult();
            SetStatus(result.Message);
        }

        private void LogoutImSession()
        {
            TerrariaYokonexRuntime? runtime = TerrariaYokonexRuntime.Instance;
            if (runtime == null)
            {
                SetStatus("退出失败: 运行时尚未初始化。");
                return;
            }

            YokonexDispatchResult result = runtime.LogoutWebSocketAsync(default).GetAwaiter().GetResult();
            SetStatus(result.Message);
        }

        private void TriggerTestEvent()
        {
            TerrariaYokonexRuntime? runtime = TerrariaYokonexRuntime.Instance;
            if (runtime == null)
            {
                SetStatus("测试失败: 运行时尚未初始化。");
                return;
            }

            string eventKey = (_testEventKeyInput?.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(eventKey))
            {
                SetStatus("测试失败: 请先填写测试事件键。");
                return;
            }

            string matchValue = (_testMatchValueInput?.Text ?? string.Empty).Trim();
            runtime.QueueEvent(TerrariaEventRecord.Create(
                eventKey,
                "图形化配置测试: " + eventKey,
                matchValue,
                0,
                matchValue));
            SetStatus("测试事件已入队: " + eventKey);
        }

        private bool ValidateBeforeSave(out string error)
        {
            foreach (Func<string?> validator in _validators)
            {
                string? message = validator();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    error = message;
                    return false;
                }
            }

            HashSet<string> ruleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (YokonexRouteRule rule in _snapshot.Routes.Rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Id))
                {
                    error = "规则 ID 不能为空。";
                    return false;
                }

                if (!ruleIds.Add(rule.Id.Trim()))
                {
                    error = "规则 ID 重复: " + rule.Id;
                    return false;
                }

                if (string.IsNullOrWhiteSpace(rule.EventKey))
                {
                    error = "规则 " + rule.Id + " 的 EventKey 不能为空。";
                    return false;
                }

                if (!string.Equals(rule.CommandId, rule.EventKey, StringComparison.OrdinalIgnoreCase))
                {
                    error = "规则 " + rule.Id + " 的 CommandId 必须与事件键一致。";
                    return false;
                }

                if (rule.Enabled && string.IsNullOrWhiteSpace(rule.CommandId))
                {
                    error = "规则 " + rule.Id + " 已启用，但 CommandId 不能为空。";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private void EnsureValidRuleSelection()
        {
            if (_snapshot.Routes == null)
            {
                _snapshot.Routes = new YokonexRouteConfig();
            }

            if (_snapshot.Routes.Rules == null)
            {
                _snapshot.Routes.Rules = new List<YokonexRouteRule>();
            }

            if (_snapshot.Settings == null)
            {
                _snapshot.Settings = new YokonexRuntimeSettings();
            }

            if (_snapshot.Settings.WebSocket == null)
            {
                _snapshot.Settings.WebSocket = new YokonexWebSocketSettings();
            }

            foreach (YokonexRouteRule rule in _snapshot.Routes.Rules)
            {
                rule.OutputMode = YokonexOutputModes.WebSocketCommand;
                rule.CommandId = rule.EventKey?.Trim() ?? string.Empty;
                rule.MatchValue = string.Empty;
            }

            if (_snapshot.Routes.Rules.Count == 0)
            {
                _selectedRuleIndex = 0;
                return;
            }

            if (_selectedRuleIndex < 0)
            {
                _selectedRuleIndex = 0;
            }
            else if (_selectedRuleIndex >= _snapshot.Routes.Rules.Count)
            {
                _selectedRuleIndex = _snapshot.Routes.Rules.Count - 1;
            }
        }

        private string BuildToolSummaryText()
        {
            TerrariaYokonexRuntime? runtime = TerrariaYokonexRuntime.Instance;
            if (runtime == null)
            {
                return "运行时尚未初始化";
            }

            return "状态摘要\n" +
                   runtime.GetStatusText() + "\n\n" +
                   "配置路径\n" +
                   runtime.ConfigDirectoryPath + "\n\n" +
                   "已支持事件\n" +
                   BuildWrappedLines(YokonexKnownValues.SupportedEventKeys, 3);
        }

        private void SetStatus(string message)
        {
            _statusMessage = message ?? string.Empty;
            if (_statusLabel != null)
            {
                _statusLabel.SetText("状态: " + _statusMessage);
            }
        }

        private static string GetOutputModeLabel()
        {
            return YokonexOutputModes.WebSocketCommand;
        }

        private static bool TryParseNonNegativeInt(string rawValue, out int value)
        {
            return int.TryParse(rawValue?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0;
        }

        private static bool IsFreshKeyPress(Keys key)
        {
            return Main.keyState.IsKeyDown(key) && Main.oldKeyState.IsKeyUp(key);
        }

        private static string TrimText(string value, int maxLength)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length <= maxLength)
            {
                return normalized;
            }

            return normalized.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }

        private static string BuildWrappedLines(IReadOnlyList<string> values, int itemsPerLine)
        {
            List<string> lines = new List<string>();
            for (int index = 0; index < values.Count; index += itemsPerLine)
            {
                int remaining = Math.Min(itemsPerLine, values.Count - index);
                string[] buffer = new string[remaining];
                for (int itemIndex = 0; itemIndex < remaining; itemIndex++)
                {
                    buffer[itemIndex] = values[index + itemIndex];
                }

                lines.Add(string.Join(" | ", buffer));
            }

            return string.Join("\n", lines);
        }

        private IReadOnlyList<ActionButtonSpec> BuildEventKeyActions(YokonexRouteRule selectedRule)
        {
            return new[]
            {
                CreateActionButtonSpec("受伤", () => ApplyEventKey(selectedRule, "player_hurt")),
                CreateActionButtonSpec("死亡", () => ApplyEventKey(selectedRule, "player_death")),
                CreateActionButtonSpec("复活", () => ApplyEventKey(selectedRule, "player_respawn")),
                CreateActionButtonSpec("Boss 出现", () => ApplyEventKey(selectedRule, "boss_spawn"), 90f),
                CreateActionButtonSpec("Boss 击败", () => ApplyEventKey(selectedRule, "boss_defeat"), 90f),
                CreateActionButtonSpec("下一个事件", () => ApplyEventKey(selectedRule, FindNextSupportedEventKey(selectedRule.EventKey)), 100f),
            };
        }

        private void ApplyEventKey(YokonexRouteRule selectedRule, string eventKey)
        {
            selectedRule.EventKey = eventKey;
            SetStatus("已填入事件键: " + eventKey);
            RebuildInterface();
        }

        private static string FindNextSupportedEventKey(string currentEventKey)
        {
            IReadOnlyList<string> supportedEventKeys = YokonexKnownValues.SupportedEventKeys;
            if (supportedEventKeys.Count == 0)
            {
                return string.Empty;
            }

            for (int index = 0; index < supportedEventKeys.Count; index++)
            {
                if (!string.Equals(supportedEventKeys[index], currentEventKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return supportedEventKeys[(index + 1) % supportedEventKeys.Count];
            }

            return supportedEventKeys[0];
        }

        private static ActionButtonSpec CreateActionButtonSpec(string text, Action onClick, float width = 86f)
        {
            return new ActionButtonSpec(text, onClick, width);
        }

        private readonly struct ActionButtonSpec
        {
            public ActionButtonSpec(string text, Action onClick, float width)
            {
                Text = text;
                OnClick = onClick;
                Width = width;
            }

            public string Text { get; }

            public Action OnClick { get; }

            public float Width { get; }
        }
    }
}
