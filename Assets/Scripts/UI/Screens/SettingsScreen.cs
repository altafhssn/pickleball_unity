using System;
using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;
using Pickleball.Backend;

namespace Pickleball.UI
{
    /// <summary>
    /// Screen 14 — settings, rebuilt against the Pickle Smash board (Docs/Figma/Frame-6.svg).
    ///
    /// The board is a flat list: back disc and title, then label-left / control-right rows sitting
    /// straight on the backdrop with hairline rules between them, then LOG OUT as plain type. No
    /// section panels, no group headings, no chrome around anything except the back button.
    ///
    /// The board names four rows — Sound Effects, Music, Notifications, Vibration. The previous
    /// screen carried more than that and all of it is still here, because none of it is decorative:
    /// swipe sensitivity and left-handed change how the game plays, and the name field is the only
    /// way to tell two installs apart in PvP. They are restyled into the board's row form rather
    /// than dropped.
    ///
    /// The two volume rows became toggles to match the board. A toggle mutes to 0 and restores the
    /// last audible level rather than forgetting it, so nothing is actually lost — see
    /// <see cref="MuteRow"/>.
    /// </summary>
    public static class SettingsScreen
    {
        /// <summary>Board: rows are pitched 64px apart on a 390-wide board.</summary>
        private static readonly float RowPitch = UITheme.F(64f);
        /// <summary>Board: the back disc ends at y 90 and the first row's centre sits at y 152, so
        /// the list starts at 120 — half a row above that centre.</summary>
        private static readonly float ContentTop = UITheme.F(120f);
        private static readonly float SidePad = UITheme.F(24f);
        private static readonly Vector2 ToggleSize = new Vector2(UITheme.F(60f), UITheme.F(28f));

        /// <summary>Notifications are a device permission, not part of the player's profile, so the
        /// preference lives in PlayerPrefs rather than in the synced save.</summary>
        private const string NotificationsKey = "ps_notifications_on";

        /// <summary>Level a muted channel comes back to when it is switched on again.</summary>
        private const float DefaultVolume = 0.7f;

        public static GameObject Build(Transform parent, ScreenManager mgr, Action onBack = null)
        {
            GameObject root = UIBuilder.Child("SettingsScreen", parent);
            UIBuilder.Fill(root);
            PSKit.Backdrop(root.transform);
            Transform safe = PSKit.BoardHost(UIBuilder.SafeArea(root.transform)).transform;

            Action back = onBack != null ? onBack : (Action)delegate { mgr.NavigateTab("HOME"); };
            GameObject header = PSKit.TopBar(safe, "SETTINGS", back);
            Text headerTitle = header.transform.Find("Title").GetComponent<Text>();
            UIReferenceArt.Title(headerTitle, "title_settings", 110.3f, 15.7f);
            RectTransform titleArt = (RectTransform)headerTitle.transform.GetChild(0);
            titleArt.anchorMin = titleArt.anchorMax = titleArt.pivot = new Vector2(0f, 0.5f);
            titleArt.anchoredPosition = Vector2.zero;

            RectTransform content;
            GameObject scroll = UIBuilder.ScrollView(safe, out content);
            UIBuilder.StretchRect(scroll, Vector2.zero, Vector2.one,
                new Vector2(SidePad, UITheme.SpaceLg), new Vector2(-SidePad, -ContentTop));

            float y = 0f;

            y = MuteRow(content, y, "Sound Effects",
                delegate { return MetaGameState.EffectsVolume; },
                delegate (float v) { MetaGameState.EffectsVolume = v; });

            y = MuteRow(content, y, "Music",
                delegate { return MetaGameState.MusicVolume; },
                delegate (float v) { MetaGameState.MusicVolume = v; });

            y = ToggleRow(content, y, "Notifications",
                PlayerPrefs.GetInt(NotificationsKey, 1) == 1,
                delegate (bool on) { PlayerPrefs.SetInt(NotificationsKey, on ? 1 : 0); PlayerPrefs.Save(); });

            y = ToggleRow(content, y, "Vibration", MetaGameState.HapticsOn,
                delegate (bool on) { MetaGameState.HapticsOn = on; });

            // Profile navigation uses the reference board's plain text action style.
            Text logout = PSKit.TextButton(content, "PLAYER PROFILE", UITheme.InkOnLight, 52,
                delegate { mgr.ShowAccount(); });
            UIBuilder.Rect(logout.transform.parent.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -(y + UITheme.F(42f))), new Vector2(0, UITheme.TouchMin));

            Text advanced = PSKit.TextButton(content, "PLAYER & CONTROLS", UITheme.InkOnLight, 30, null);
            UIBuilder.Rect(advanced.transform.parent.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -UITheme.F(360f)), new Vector2(0, UITheme.TouchMin));
            GameObject extra = UIBuilder.Child("AdvancedSettings", content);
            UIBuilder.StretchRect(extra, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0, -UITheme.F(670f)), new Vector2(0, -UITheme.F(410f)));
            float extraY = ToggleRow(extra.transform, 0f, "Left-handed", MetaGameState.LeftHanded,
                delegate (bool on) { MetaGameState.LeftHanded = on; });
            extraY = SliderRow(extra.transform, extraY, "Swipe sensitivity", MetaGameState.SwipeSensitivity,
                delegate (float v) { MetaGameState.SwipeSensitivity = v; });
            extraY = NameRow(extra.transform, extraY);
            ValueRow(extra.transform, extraY, "Player ID", FormatGuestId(), false);
            extra.SetActive(false);
            advanced.transform.parent.GetComponent<Button>().onClick.AddListener(delegate
            {
                bool show = !extra.activeSelf;
                extra.SetActive(show);
                advanced.text = show ? "HIDE PLAYER & CONTROLS" : "PLAYER & CONTROLS";
                content.sizeDelta = new Vector2(0, UITheme.F(show ? 720f : 500f));
            });
            content.sizeDelta = new Vector2(0, UITheme.F(500f));
            Text version = PSKit.Body(safe, "Version", TextAnchor.MiddleCenter,
                "v" + Application.version, UITheme.InkOnLightDim, 24);
            PSKit.BoardRow(version.gameObject, 798f, 20f);
            return root;
        }

        // ============================================================
        // ROWS
        // ============================================================

        /// <summary>Row shell: full-width host at <paramref name="y"/>, plus the rule under it.
        /// Returns the y for the next row.</summary>
        private static GameObject RowHost(Transform content, float y, out float nextY)
        {
            GameObject row = UIBuilder.Child("Row", content);
            UIBuilder.StretchRect(row, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0, -(y + RowPitch)), new Vector2(0, -y));
            PSKit.Rule(content, y + RowPitch);
            nextY = y + RowPitch;
            return row;
        }

        private static Text RowLabel(GameObject row, string label)
        {
            Text t = PSKit.Body(row.transform, "Label", TextAnchor.MiddleLeft, label, UITheme.InkOnLight, 52);
            UIBuilder.StretchRect(t.gameObject, new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, new Vector2(-ToggleSize.x - UITheme.SpaceMd, 0));
            UIBuilder.ClampLine(t, 22);
            return t;
        }

        private static float ToggleRow(Transform content, float y, string label, bool on, Action<bool> onChanged)
        {
            float next;
            GameObject row = RowHost(content, y, out next);
            RowLabel(row, label);

            GameObject toggle = PSKit.Toggle(row.transform, ToggleSize, on, onChanged);
            UIBuilder.Rect(toggle, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                Vector2.zero, ToggleSize);
            return next;
        }

        /// <summary>
        /// A volume channel expressed as the board's on/off toggle. Muting stores the level it was
        /// at so switching back on restores it — a toggle that always came back at full volume
        /// would be a worse control than the slider it replaced, not a simpler one.
        /// </summary>
        private static float MuteRow(Transform content, float y, string label, Func<float> get, Action<float> set)
        {
            string key = "ps_last_vol_" + label.Replace(" ", "_");
            return ToggleRow(content, y, label, get() > 0.001f, delegate (bool on)
            {
                if (on)
                {
                    set(PlayerPrefs.GetFloat(key, DefaultVolume));
                }
                else
                {
                    float current = get();
                    if (current > 0.001f) PlayerPrefs.SetFloat(key, current);
                    set(0f);
                }
                PlayerPrefs.Save();
            });
        }

        private static float SliderRow(Transform content, float y, string label, float value, Action<float> onChanged)
        {
            float next;
            GameObject row = RowHost(content, y, out next);

            Text name = PSKit.Body(row.transform, "Label", TextAnchor.MiddleLeft, label, UITheme.InkOnLight, 52);
            UIBuilder.StretchRect(name.gameObject, new Vector2(0f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            UIBuilder.ClampLine(name, 22);

            Text readout = PSKit.Body(row.transform, "Readout", TextAnchor.MiddleRight, "", UITheme.InkOnLight, 26);
            UIBuilder.Rect(readout.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0, UITheme.F(11f)), new Vector2(110, 34));

            GameObject sliderHost = UIBuilder.Child("Slider", row.transform);
            UIBuilder.Rect(sliderHost, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0, -UITheme.F(6f)), new Vector2(UITheme.F(120f), UITheme.F(15f)));
            UISliderWidget slider = UISliderWidget.Build(sliderHost, value, UITheme.Volt, onChanged);
            slider.BindLabel(readout);
            return next;
        }

        /// <summary>
        /// Editable player name. Kept from the previous screen: <see cref="MetaGameState.PlayerName"/>
        /// otherwise stays on its hardcoded default, and two installs on the same person's devices —
        /// exactly how PvP gets tested solo — then show identical opponent names in a real match.
        /// </summary>
        private static float NameRow(Transform content, float y)
        {
            float next;
            GameObject row = RowHost(content, y, out next);

            Text label = PSKit.Body(row.transform, "Label", TextAnchor.MiddleLeft, "Name", UITheme.InkOnLight, 34);
            UIBuilder.Rect(label.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(200, 50));

            GameObject fieldBg = UIBuilder.Child("NameField", row.transform);
            UIBuilder.StretchRect(fieldBg, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(220, -UITheme.F(17f)), new Vector2(0, UITheme.F(17f)));
            UIBuilder.ExpandHitArea(fieldBg, new Vector2(0f, UITheme.F(34f)));
            Image bg = fieldBg.AddComponent<Image>();
            bg.sprite = UIBuilder.CapsuleSprite;
            bg.gameObject.AddComponent<UICapsuleFit>();
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.Panel;

            Text fieldText = PSKit.Body(fieldBg.transform, "Text", TextAnchor.MiddleRight,
                MetaGameState.PlayerName, UITheme.Cream, 30);
            fieldText.raycastTarget = false;   // taps fall through to the InputField's own hit target
            UIBuilder.StretchRect(fieldText.gameObject, Vector2.zero, Vector2.one, new Vector2(24, 4), new Vector2(-24, -4));

            Text placeholder = PSKit.Body(fieldBg.transform, "Placeholder", TextAnchor.MiddleRight,
                "Enter name", new Color(1f, 0.976f, 0.918f, 0.4f), 30);
            placeholder.raycastTarget = false;
            UIBuilder.StretchRect(placeholder.gameObject, Vector2.zero, Vector2.one, new Vector2(24, 4), new Vector2(-24, -4));

            InputField field = fieldBg.AddComponent<InputField>();
            field.targetGraphic = bg;
            // Explicit ColorBlock: the default crossfades toward opaque white, which washes out a
            // near-black field entirely. On touch this tint is also the only confirmation a tap
            // landed, since no caret appears until the OS keyboard opens a frame later.
            ColorBlock colors = field.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
            colors.selectedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            field.colors = colors;
            field.textComponent = fieldText;
            field.placeholder = placeholder;
            field.text = MetaGameState.PlayerName;
            field.characterLimit = 16;
            field.lineType = InputField.LineType.SingleLine;

            field.onEndEdit.AddListener(delegate (string value)
            {
                string trimmed = value != null ? value.Trim() : "";
                MetaGameState.PlayerName = string.IsNullOrEmpty(trimmed) ? MetaGameState.PlayerName : trimmed;
                field.text = MetaGameState.PlayerName;
            });
            return next;
        }

        private static float ValueRow(Transform content, float y, string label, string value, bool chevron)
        {
            float next;
            GameObject row = RowHost(content, y, out next);

            Text name = PSKit.Body(row.transform, "Label", TextAnchor.MiddleLeft, label, UITheme.InkOnLight, 52);
            UIBuilder.StretchRect(name.gameObject, new Vector2(0f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            UIBuilder.ClampLine(name, 22);

            Text val = PSKit.Body(row.transform, "Value", TextAnchor.MiddleRight, value,
                UITheme.InkOnLightMute, 30);
            UIBuilder.StretchRect(val.gameObject, new Vector2(0.4f, 0f), new Vector2(1f, 1f),
                Vector2.zero, new Vector2(chevron ? -UITheme.F(20f) : 0f, 0));
            UIBuilder.ClampLine(val, 20);

            if (chevron)
            {
                UIBuilder.IconAt(row.transform, IconId.ChevronRight, 36f, UITheme.InkOnLightMute,
                    new Color(0, 0, 0, 0), new Vector2(1f, 0.5f), Vector2.zero);
            }
            return next;
        }

        /// <summary>Shortened, readable form of the real per-install guest id (see
        /// <see cref="ProfileService.GuestId"/>) — the full GUID is unwieldy to display.</summary>
        private static string FormatGuestId()
        {
            string id = ProfileService.Instance != null ? ProfileService.Instance.GuestId : null;
            if (string.IsNullOrEmpty(id)) return "----------";

            string hex = id.Replace("-", "").ToUpperInvariant();
            return hex.Length >= 8 ? hex.Substring(0, 4) + "-" + hex.Substring(4, 4) : hex;
        }
    }
}
