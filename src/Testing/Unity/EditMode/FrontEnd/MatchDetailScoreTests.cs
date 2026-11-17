using System;
using System.Reflection;
using NUnit.Framework;
using PickleVerse.FrontEnd;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PickleVerse.Tests.FrontEnd
{
    public sealed class MatchDetailScoreTests
    {
        const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(1, -1)]
        [TestCase(1, 1)]
        [TestCase(2, -1)]
        public void ScoreColumnsRenderEveryPlayerName(int playersPerTeam, int teamAStartSide)
        {
            var host = new GameObject("Score test menu");
            host.SetActive(false);
            var canvas = new GameObject("Score test canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var menu = host.AddComponent<PlayerProgressionMenu>();
                menu.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/FrontEndContent/Mode Vietnamese Font.asset");
                Assert.That(menu.font, Is.Not.Null);
                menu.window = (RectTransform)canvas.transform;
                menu.window.sizeDelta = new Vector2(1280, 720);

                Type menuType = typeof(PlayerProgressionMenu);
                menuType.GetMethod("BuildMatchDetail", InstancePrivate).Invoke(menu, null);
                var render = menuType.GetMethod("RenderGameScore", InstancePrivate);
                var parameters = render.GetParameters();
                Type gameType = parameters[0].ParameterType;
                var game = Activator.CreateInstance(gameType, true);
                gameType.GetField("gameNumber").SetValue(game, 2);
                gameType.GetField("teamAStartSide").SetValue(game, teamAStartSide);
                gameType.GetField("completed").SetValue(game, true);
                Type playerType = parameters[1].ParameterType.GetElementType();
                var players = Array.CreateInstance(playerType, playersPerTeam * 2);
                for (int i = 0; i < players.Length; i++)
                {
                    var player = Activator.CreateInstance(playerType, true);
                    playerType.GetField("name").SetValue(player, $"Người chơi {i + 1}");
                    playerType.GetField("team").SetValue(player, i / playersPerTeam);
                    playerType.GetField("canReport").SetValue(player, i == playersPerTeam);
                    players.SetValue(player, i);
                }
                render.Invoke(menu, new[] { game, players });

                var content = (RectTransform)menu.window.Find("MatchDetail/DetailScroll/Viewport/Content");
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                Canvas.ForceUpdateCanvases();
                var row = content.Find("Game2");
                Assert.That(row.GetChild(1).name, Is.EqualTo(teamAStartSide == 1 ? "ĐỘI B" : "ĐỘI A"));
                for (int i = 0; i < players.Length; i++)
                {
                    string team = i < playersPerTeam ? "ĐỘI A" : "ĐỘI B";
                    var label = row.Find($"{team}/Player{i % playersPerTeam}").GetComponent<TMP_Text>();
                    label.ForceMeshUpdate();
                    Assert.That(label.text, Is.EqualTo($"Người chơi {i + 1}"));
                    Assert.That(label.isTextTruncated, Is.False,
                        $"{label.text} needs {label.preferredHeight:F2} units and has {label.rectTransform.rect.height:F2}.");
                    Assert.That(label.textInfo.characterCount, Is.EqualTo(label.text.Length));
                    Assert.That(label.textInfo.characterInfo[0].isVisible, Is.True);
                }
                var report = row.Find("ĐỘI B/ReportPlayer0/Label").GetComponent<TMP_Text>();
                report.ForceMeshUpdate();
                Assert.That(report.isTextTruncated, Is.False);
                Assert.That(report.textInfo.characterCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
                Object.DestroyImmediate(host);
            }
        }
    }
}
