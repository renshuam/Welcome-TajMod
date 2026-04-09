using BepInEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace WelcomePlugin
{
    [BepInPlugin("com.yourname.welcomeplugin", "Welcome Plugin", "1.0.0")]
    [BepInProcess("SCPSL.exe")]
    public class WelcomePlugin : BaseUnityPlugin
    {
        private HashSet<string> _currentPlayers = new HashSet<string>();
        private float _timer = 0f;
        private const float CHECK_INTERVAL = 2f;

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= CHECK_INTERVAL)
            {
                _timer = 0f;
                CheckForNewPlayers();
            }
        }

        private void CheckForNewPlayers()
        {
            var players = GetAllPlayers();
            if (players == null || players.Count == 0) return;

            var newPlayers = players.Keys.Except(_currentPlayers).ToList();
            foreach (var steamId in newPlayers)
            {
                string playerName = players[steamId];
                Debug.Log($"[WelcomePlugin] 新玩家: {playerName} ({steamId})");
                StartCoroutine(DelayedWelcome(playerName)); // 延迟1秒确保玩家初始化
            }

            _currentPlayers = new HashSet<string>(players.Keys);
        }

        private IEnumerator DelayedWelcome(string playerName)
        {
            yield return new WaitForSeconds(1f);
            SendWelcomeBroadcast(playerName);
        }

        private Dictionary<string, string> GetAllPlayers()
        {
            var result = new Dictionary<string, string>();
            try
            {
                Type hubType = Type.GetType("ReferenceHub, Assembly-CSharp");
                if (hubType == null) return result;

                PropertyInfo allHubsProp = hubType.GetProperty("AllHubs", BindingFlags.Public | BindingFlags.Static);
                if (allHubsProp == null) return result;

                var hubs = allHubsProp.GetValue(null) as IEnumerable<object>;
                if (hubs == null) return result;

                foreach (var hub in hubs)
                {
                    string name = GetPlayerName(hub);
                    string id = GetPlayerSteamId(hub);
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                        result[id] = name;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[WelcomePlugin] 获取玩家列表失败: {e}");
            }
            return result;
        }

        private string GetPlayerName(object hub)
        {
            if (hub == null) return "Unknown";
            var prop = hub.GetType().GetProperty("Nickname");
            if (prop != null)
            {
                var val = prop.GetValue(hub);
                if (val != null) return val.ToString();
            }
            prop = hub.GetType().GetProperty("Username");
            if (prop != null)
            {
                var val = prop.GetValue(hub);
                if (val != null) return val.ToString();
            }
            return "Player";
        }

        private string GetPlayerSteamId(object hub)
        {
            try
            {
                var authField = hub.GetType().GetField("_authManager", BindingFlags.NonPublic | BindingFlags.Instance);
                if (authField != null)
                {
                    var auth = authField.GetValue(hub);
                    if (auth != null)
                    {
                        var userIdProp = auth.GetType().GetProperty("UserId");
                        if (userIdProp != null)
                        {
                            var userId = userIdProp.GetValue(auth);
                            if (userId != null) return userId.ToString();
                        }
                    }
                }
            }
            catch { }
            return hub.GetHashCode().ToString();
        }

        private void SendWelcomeBroadcast(string playerName)
        {
            string message = $"欢迎{playerName}来到服务器希望你玩得开心";
            uint duration = 10;
            bool monospaced = false;

            try
            {
                Type broadcastType = Type.GetType("Broadcast, Assembly-CSharp");
                if (broadcastType == null) return;

                // 方法1：通过 FindObjectOfType 获取 Broadcast 实例并调用 RpcAddElement
                var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new[] { typeof(Type) });
                if (findMethod != null)
                {
                    object broadcastInstance = findMethod.Invoke(null, new object[] { broadcastType });
                    if (broadcastInstance != null)
                    {
                        MethodInfo rpcMethod = broadcastType.GetMethod("RpcAddElement", new[] { typeof(string), typeof(uint), typeof(bool) });
                        if (rpcMethod != null)
                        {
                            rpcMethod.Invoke(broadcastInstance, new object[] { message, duration, monospaced });
                            Debug.Log($"[WelcomePlugin] 已通过 RpcAddElement 发送广播到所有客户端");
                            return;
                        }
                    }
                }

                // 方法2：如果找不到实例，回退到静态 AddElement（仅服务器日志）
                MethodInfo addElement = broadcastType.GetMethod("AddElement", new[] { typeof(string), typeof(uint), typeof(bool) });
                if (addElement != null)
                {
                    addElement.Invoke(null, new object[] { message, duration, monospaced });
                    Debug.Log($"[WelcomePlugin] 已通过 AddElement 发送广播（仅服务器日志）");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[WelcomePlugin] 发送广播失败: {e}");
            }
        }
    }
}