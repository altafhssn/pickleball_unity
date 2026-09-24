#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Pickleball.Data;

namespace Pickleball.UI
{
    // Editor-only visual regression sweep. Does not invoke purchase, upgrade or claim actions.
    public class UITextLayoutAudit : MonoBehaviour
    {
        public static bool Done;
        public static string Report;
        public void Run() { Done = false; StartCoroutine(Sweep()); }

        private IEnumerator Sweep()
        {
            var mgr = ScreenManager.Instance;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ScreenManager).GetField("selectedGear", flags).SetValue(mgr, MetaGameState.Gear[0]);
            typeof(ScreenManager).GetField("selectedBag", flags).SetValue(mgr, int.MaxValue);
            var rows = new List<string>();
            string dir = "output/text-audit/" + Screen.width + "x" + Screen.height;
            Directory.CreateDirectory(dir);
            var screens = new[] { ScreenId.Boot, ScreenId.Lobby, ScreenId.PlayMode, ScreenId.TourSelect,
                ScreenId.BagInventory, ScreenId.GearLoadout, ScreenId.GearCatalog, ScreenId.GearDetail,
                ScreenId.GearUpgradeReveal, ScreenId.League, ScreenId.SeasonPass, ScreenId.Shop,
                ScreenId.Settings, ScreenId.Account, ScreenId.Purchase, ScreenId.ProfileRecovery,
                ScreenId.Tutorial, ScreenId.StartupStanding, ScreenId.StartupLeagueResult,
                ScreenId.SeasonComplete, ScreenId.ChestOpening, ScreenId.Matchmaking, ScreenId.MatchIntro };
            foreach (var screen in screens)
            {
                mgr.Show(screen, false);
                mgr.DismissModal();
                // Some reference-board screens resolve their fit one LateUpdate after the screen
                // transition. A little headroom keeps captures deterministic on a busy editor.
                yield return new WaitForSecondsRealtime(.75f);
                yield return new WaitForEndOfFrame();
                Canvas.ForceUpdateCanvases();
                var root = (GameObject)typeof(ScreenManager).GetField("currentScreenGO", flags).GetValue(mgr);
                Capture(root, screen.ToString(), dir, rows);
            }
            mgr.EnterMatch();
            yield return new WaitForSecondsRealtime(.2f);
            yield return new WaitForEndOfFrame();
            Capture((GameObject)typeof(GameplayHUD).GetField("canvasRoot", flags).GetValue(GameplayHUD.Instance), "GameplayHUD", dir, rows);
            mgr.ShowPause();
            yield return new WaitForSecondsRealtime(.3f);
            yield return new WaitForEndOfFrame();
            Capture((GameObject)typeof(ScreenManager).GetField("currentScreenGO", flags).GetValue(mgr), "Pause", dir, rows);
            mgr.ConfirmForfeit();
            yield return new WaitForSecondsRealtime(.3f);
            yield return new WaitForEndOfFrame();
            Capture((GameObject)typeof(ScreenManager).GetField("modalGO", flags).GetValue(mgr), "Confirmation", dir, rows);
            mgr.DismissModal();
            mgr.ShowSettingsFromPause();
            var settingsRoot = (GameObject)typeof(ScreenManager).GetField("currentScreenGO", flags).GetValue(mgr);
            foreach (var text in settingsRoot.GetComponentsInChildren<Text>())
                if (text.text == "PLAYER & CONTROLS") text.GetComponentInParent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.3f);
            yield return new WaitForEndOfFrame();
            Capture(settingsRoot, "SettingsExpanded", dir, rows);
            mgr.ResumeFromPause();
            GameplayHUD.Instance.ShowMatchResult(true);
            yield return new WaitForSecondsRealtime(.3f);
            yield return new WaitForEndOfFrame();
            Capture((GameObject)typeof(GameplayHUD).GetField("canvasRoot", flags).GetValue(GameplayHUD.Instance), "MatchResult", dir, rows);
            Report = string.Join("\n", rows.ToArray());
            File.WriteAllText(dir + "/findings.txt", Report);
            mgr.ExitMatchToLobby();
            Done = true;
            Destroy(this);
        }
        private static void Capture(GameObject root, string screen, string dir, List<string> rows)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var t in root.GetComponentsInChildren<Text>())
            {
                if (!t.isActiveAndEnabled || string.IsNullOrWhiteSpace(t.text)) continue;
                if (t.text.IndexOf('\uFFFD') >= 0)
                    rows.Add(screen + " | invalid replacement character | " + t.name + " | " + t.text);

                var rect = t.rectTransform.rect;
                if (rect.width <= 0 || rect.height <= 0) continue;
                var settings = t.GetGenerationSettings(rect.size);
                settings.resizeTextForBestFit = false;
                if (t.resizeTextForBestFit && t.cachedTextGenerator.fontSizeUsedForBestFit > 0)
                    settings.fontSize = t.cachedTextGenerator.fontSizeUsedForBestFit;
                var generator = new TextGenerator();
                float needed = generator.GetPreferredHeight(t.text, settings) / t.pixelsPerUnit;
                float width = generator.GetPreferredWidth(t.text, settings) / t.pixelsPerUnit;
                if (needed > rect.height + 2 || (t.horizontalOverflow == HorizontalWrapMode.Overflow && width > rect.width + 2))
                    rows.Add(screen + " | text overflow | " + t.name + " | " + t.text.Replace("\n", " / ") + " | font=" + settings.fontSize
                        + " | rect=" + rect.size + " needed=" + width.ToString("F1") + "x" + needed.ToString("F1"));
            }

            foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>())
                AuditTouchTarget(selectable.transform as RectTransform, screen, selectable.name, rows);
            foreach (PSToggle toggle in root.GetComponentsInChildren<PSToggle>())
                AuditTouchTarget(toggle.transform as RectTransform, screen, toggle.name, rows);
            foreach (UISliderWidget slider in root.GetComponentsInChildren<UISliderWidget>())
                AuditTouchTarget(slider.transform as RectTransform, screen, slider.name, rows);
            foreach (UIToggleWidget toggle in root.GetComponentsInChildren<UIToggleWidget>())
                AuditTouchTarget(toggle.transform as RectTransform, screen, toggle.name, rows);

            var image = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(dir + "/" + screen + ".png", image.EncodeToPNG());
            Destroy(image);
        }

        private static void AuditTouchTarget(RectTransform control, string screen, string name, List<string> rows)
        {
            if (control == null || !control.gameObject.activeInHierarchy) return;
            Transform hit = control.Find("HitArea");
            RectTransform effective = hit != null ? hit as RectTransform : control;
            if (effective == null) return;

            Vector2 size = effective.rect.size;
            if (size.x + 1f < UITheme.TouchMin || size.y + 1f < UITheme.TouchMin)
            {
                rows.Add(screen + " | undersized touch target | " + name + " | rect="
                    + size.x.ToString("F1") + "x" + size.y.ToString("F1")
                    + " min=" + UITheme.TouchMin);
            }
        }

    }
}
#endif
