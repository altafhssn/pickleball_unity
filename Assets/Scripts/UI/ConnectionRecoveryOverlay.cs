using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Pickleball.UI
{
    public class ConnectionRecoveryOverlay : MonoBehaviour
    {
        public static GameObject Build(Transform parent, ScreenManager mgr, bool local)
        {
            GameObject root;
            Transform board = FlowScreens.Shell(parent, "ConnectionRecovery", "MATCH CONNECTION", null, out root);
            // Full-screen backdrop blocks match gestures until authoritative play can resume.
            root.AddComponent<Image>().color = Color.clear;
            GameObject ball = PSKit.BallMark(board, UITheme.F(58));
            PSKit.BoardDisc(ball, 225, 58);
            UIRingPulse.Attach(ball, 0);
            FlowScreens.Label(board, local ? "RECONNECTING..." : "OPPONENT DISCONNECTED", 337, 78, true);
            Text detail = FlowScreens.Label(board, "", 440, 96);
            GameObject exit = UIBuilder.Child("ExitActions", board);
            UIBuilder.Fill(exit);
            FlowScreens.ActionButton(exit.transform, "BACK TO HOME", 653, mgr.ForfeitMatch, true);
            exit.SetActive(false);
            root.AddComponent<ConnectionRecoveryOverlay>().StartCoroutine(Countdown(local, detail, exit));
            return root;
        }

        private static IEnumerator Countdown(bool local, Text detail, GameObject exit)
        {
            float until = Time.unscaledTime + 15f;
            while (Time.unscaledTime < until)
            {
                detail.text = (local ? "Trying to restore your match..." : "Giving your opponent time to return...")
                    + "\n" + Mathf.CeilToInt(until - Time.unscaledTime) + " seconds";
                yield return null;
            }
            if (local)
            {
                detail.text = "The connection could not be restored.\nReturning home counts as a disconnect loss.";
                exit.SetActive(true);
            }
            else detail.text = "Resolving the match...";
        }
    }
}
