using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Pickleball.Gameplay;
using Pickleball.Net;
#if PHOTON_UNITY_NETWORKING
using Photon.Realtime;
using Photon.Pun;
using ExitGames.Client.Photon;
#endif

namespace Pickleball.EditorTools
{
    public static class MultiplayerAudit
    {
        public static string CheckBallCatchUp()
        {
            var go = new GameObject("NetworkBallAudit");
            try
            {
                var ball = go.AddComponent<BallController>();
                var shot = new ShotData(1, ShotType.Topspin, new Vector3(0, 1, 6), new Vector3(0, 0, -7), 1.2f, 1f);
                int checks = 0;
                foreach (float elapsed in new[] { 0f, .08f, .15f, .3f, .75f, 1.1f, 1.3f })
                {
                    int bounces = 0;
                    Action<Vector3> bounce = _ => bounces++;
                    ball.OnBallBounced += bounce;
                    ball.LaunchShot(shot);
                    ball.AdvanceToElapsed(elapsed);
                    Vector3 expected;
                    if (elapsed < shot.duration) expected = ball.CalculateParabolicPosition(shot.startPosition, shot.targetPosition, shot.arcHeight, elapsed / shot.duration);
                    else
                    {
                        Pickleball.Sim.BallFlight.Rebound(new System.Numerics.Vector3(0,1,6), new System.Numerics.Vector3(0,0,-7),
                            shot.arcHeight, shot.duration, 1f, 0f, out var end, out float arc, out float seconds);
                        expected = ball.CalculateParabolicPosition(shot.targetPosition, new Vector3(end.X,end.Y,end.Z), arc, (elapsed-shot.duration)/seconds);
                    }
                    if (Vector3.Distance(expected, ball.transform.position) > .0001f) throw new Exception("Network flight diverged at " + elapsed);
                    if (ball.CurrentShot.duration != shot.duration || ball.CurrentShot.startPosition != shot.startPosition) throw new Exception("Original shot mutated");
                    if (bounces != (elapsed < 1f ? 0 : 1)) throw new Exception("Incorrect bounce catch-up");
                    ball.AdvanceToElapsed(elapsed);
                    if (bounces > 1) throw new Exception("Duplicate bounce callback");
                    ball.OnBallBounced -= bounce;
                    checks++;
                }
                return checks + " exact flight/rebound catch-up cases passed (0–1300 ms), including one-shot bounce events.";
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

#if PHOTON_UNITY_NETWORKING
        private static LoadBalancingClient a, b;
        private static int stage, received, sent;
        private static double deadline, nextSend;
        private static string room;
        public static string CloudResult { get; private set; } = "Not run";

        public static string StartCloudCheck()
        {
            StopCloudCheck();
            a = new LoadBalancingClient(); b = new LoadBalancingClient();
            var settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
            settings.AppVersion = "pickleball-private-audit-v3";
            if (string.IsNullOrEmpty(settings.FixedRegion)) settings.FixedRegion = "asia";
            room = "audit-" + Guid.NewGuid().ToString("N");
            a.AuthValues = new AuthenticationValues(Guid.NewGuid().ToString("N"));
            b.AuthValues = new AuthenticationValues(Guid.NewGuid().ToString("N"));
            a.EventReceived += e => { if (e.Code == 91) received++; };
            b.EventReceived += e => {
                if (e.Code == 90) b.OpRaiseEvent(91, e.CustomData, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
            };
            stage = 0; received = 0; sent = 0;
            deadline = EditorApplication.timeSinceStartup + 75;
            CloudResult = "Connecting two isolated Photon clients";
            if (!a.ConnectUsingSettings(settings) || !b.ConnectUsingSettings(settings)) throw new Exception("Photon connection could not start");
            EditorApplication.update += CloudTick;
            return CloudResult;
        }

        private static void CloudTick()
        {
            try
            {
                a.Service(); b.Service();
                if (EditorApplication.timeSinceStartup > deadline) { Finish("FAILED: timeout at stage " + stage + ", states " + a.State + "/" + b.State + ", echoed " + received); return; }
                if (stage == 0 && a.State == ClientState.ConnectedToMasterServer)
                {
                    a.OpCreateRoom(new EnterRoomParams { RoomName = room, RoomOptions = new RoomOptions { MaxPlayers = 2, IsVisible = false, PlayerTtl = 15000, EmptyRoomTtl = 1000 } });
                    stage = 1;
                }
                if (stage == 1 && a.InRoom && b.State == ClientState.ConnectedToMasterServer)
                { b.OpJoinRoom(new EnterRoomParams { RoomName = room }); stage = 2; }
                if (stage == 2 && a.InRoom && b.InRoom) { stage = 3; nextSend = 0; }
                if ((stage == 3 || stage == 6) && sent < (stage == 3 ? 20 : 30) && EditorApplication.timeSinceStartup >= nextSend)
                {
                    a.OpRaiseEvent(90, new object[] { sent, "shot-commit-audit" }, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
                    sent++; nextSend = EditorApplication.timeSinceStartup + .1;
                }
                if (stage == 3 && received == 20) { b.Disconnect(); stage = 4; }
                if (stage == 4 && b.State == ClientState.Disconnected)
                { if (!b.ReconnectAndRejoin()) throw new Exception("Rejoin could not start"); stage = 5; }
                if (stage == 5 && b.InRoom) { stage = 6; CloudResult = "Rejoined; verifying post-reconnect events"; }
                if (stage == 6 && received == 30)
                    Finish("PASS: two real Photon clients joined a private room; 30/30 reliable round trips; disconnect/rejoin and post-rejoin delivery passed. RTT " + a.LoadBalancingPeer.RoundTripTime + "/" + b.LoadBalancingPeer.RoundTripTime + " ms.");
            }
            catch (Exception e) { Finish("FAILED: " + e.GetType().Name + " " + e.Message); }
        }
        private static void Finish(string result)
        {
            CloudResult = result;
            Directory.CreateDirectory("output/multiplayer-audit");
            File.WriteAllText("output/multiplayer-audit/photon-cloud.txt", result);
            StopCloudCheck();
        }
        private static void StopCloudCheck()
        {
            EditorApplication.update -= CloudTick;
            if (a != null) { a.Disconnect(); a.Service(); }
            if (b != null) { b.Disconnect(); b.Service(); }
        }
#endif
    }
}
