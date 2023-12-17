using System.Reflection;
using BepInEx;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace FrequencyWalkie
{
    public static class Patches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            MethodInfo originalSetLocalClientSpeaking = AccessTools.Method(typeof(WalkieTalkie), "SetLocalClientSpeaking");
            MethodInfo patchSetLocalClientSpeaking = AccessTools.Method(typeof(Patches), "WalkieTalkie_SetLocalClientSpeaking");
            harmony.Patch(originalSetLocalClientSpeaking, new HarmonyMethod(patchSetLocalClientSpeaking));
            
            MethodInfo original__rpc_handler_64994802 = AccessTools.Method(typeof(WalkieTalkie), "__rpc_handler_64994802");
            MethodInfo patch__rpc_handler_64994802 = AccessTools.Method(typeof(Patches), "WalkieTalkie_HandlePlayerSpeakingServerRPC");
            harmony.Patch(original__rpc_handler_64994802, new HarmonyMethod(patch__rpc_handler_64994802));
            
            MethodInfo original__rpc_handler_2961867446 = AccessTools.Method(typeof(WalkieTalkie), "__rpc_handler_2961867446");
            MethodInfo patch__rpc_handler_2961867446 = AccessTools.Method(typeof(Patches), "WalkieTalkie_HandlePlayerSpeakingClientRPC");
            harmony.Patch(original__rpc_handler_2961867446, new HarmonyMethod(patch__rpc_handler_2961867446));
            
            MethodInfo originUpdate = AccessTools.Method(typeof(WalkieTalkie), "Update");
            MethodInfo patchUpdate = AccessTools.Method(typeof(Patches), "WalkieTalkie_Update");
            harmony.Patch(originUpdate, null, new HarmonyMethod(patchUpdate));
            
            MethodInfo originStart = AccessTools.Method(typeof(WalkieTalkie), "Start");
            MethodInfo patchStart = AccessTools.Method(typeof(Patches), "WalkieTalkie_Start");
            harmony.Patch(originStart, null, new HarmonyMethod(patchStart));
            
            MethodInfo originSwitchWalkieTalkieOn = AccessTools.Method(typeof(WalkieTalkie), "SwitchWalkieTalkieOn");
            MethodInfo patchSwitchWalkieTalkieOn = AccessTools.Method(typeof(Patches), "WalkieTalkie_SwitchWalkieTalkieOn");
            harmony.Patch(originSwitchWalkieTalkieOn, null, new HarmonyMethod(patchSwitchWalkieTalkieOn));
            
            MethodInfo originEquipItem = AccessTools.Method(typeof(WalkieTalkie), "EquipItem");
            MethodInfo patchEquipItem = AccessTools.Method(typeof(Patches), "WalkieTalkie_EquipItem");
            harmony.Patch(originEquipItem, null, new HarmonyMethod(patchEquipItem));
        }
        
        private static void WalkieTalkie_EquipItem(WalkieTalkie __instance)
        {
            // Only shown once
            HUDManager.Instance.DisplayTip("FrequencyWalkie", "Press [R] or [F] to cycle between frequencies. BR.d will broadcast to all walkie talkies.", false, true, "FW_EquipTip");

            // check if another walkie talkie in our inventory && being used
            for (int i = 0; i < __instance.playerHeldBy.ItemSlots.Length; i++)
            {
                if (__instance.playerHeldBy.ItemSlots[i] == __instance && __instance.isBeingUsed) {
                    __instance.gameObject.GetComponent<Canvas>().enabled = true;
                    continue;
                }
                
                if (__instance.playerHeldBy.ItemSlots[i] == __instance)
                    continue;
                
                // if we have another walkie talkie in our inventory and it's being used
                // we disable the canvas (fixes double canvas bug)
                if (__instance.playerHeldBy.ItemSlots[i] is WalkieTalkie walkie)
                {
                    walkie.gameObject.GetComponent<Canvas>().enabled = false;
                }
            }
        }
        
        private static void WalkieTalkie_Start(WalkieTalkie __instance)
        {
            // add walkie talkie to the dictionary
            FrequencyWalkie.walkieTalkieFrequencies.Add(__instance.GetInstanceID(), 0);
            
            // add control tip to the walkie talkie
            __instance.itemProperties.toolTips = __instance.itemProperties.toolTips.AddToArray(FrequencyWalkie.tooltip);
            
            // we patch the material
            // copy texture to a render texture and modify it
            RenderTexture rt = new RenderTexture(__instance.onMaterial.mainTexture.width,
                __instance.onMaterial.mainTexture.width, 32);
            Graphics.Blit(__instance.onMaterial.mainTexture, rt);
            RenderTexture.active = rt;

            // set pixels
            Texture2D tex = new Texture2D(__instance.onMaterial.mainTexture.width,
                __instance.onMaterial.mainTexture.height);
            tex.ReadPixels(
                new Rect(0, 0, tex.width, tex.height), 0,
                0);
            var colors = new Color[tex.width * tex.height];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = new Color(0x7A, 0x96, 0x79);
            }
            tex.SetPixels(420, 64, 135, 76, colors);
            tex.Apply();
            
            RenderTexture.active = null;

            // set texture
            __instance.onMaterial.mainTexture = tex;

            // var mesh = __instance.mainObjectRenderer.GetComponent<MeshFilter>().mesh;
            // var uv = CalculateUVFromPixelCoordinates(tex, 480, 64);
            // var pos = MapUVtoObjectSpace(uv, mesh);
            
            // create a new object with a canvas renderer and a text
            // and add it as a child to the light
            var canvas = __instance.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            
            // set canvas size
            var rectTransform = canvas.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(40, 40);
            
            var text = new GameObject("Text");
            text.transform.parent = canvas.transform;
            text.transform.localPosition = new Vector3(2.7f, -0.9f, -0.23f);
            // rotate the text upwards
            // and make horizontal
            text.transform.localRotation = Quaternion.Euler(-90, -90, 0);
            
            var circle = new GameObject("Circle");
            circle.transform.parent = canvas.transform;
            circle.transform.localPosition = new Vector3(3.25f, -0.9f, -0.45f);
            circle.transform.localRotation = Quaternion.Euler(-90, -90, 0);
            circle.transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);
            
            var circleImage = circle.AddComponent<Image>();
            circleImage.sprite = Assets.GetResource<Sprite>("scanCircle2");;
            circleImage.color = Color.black;
            
            var speaking = new GameObject("Speaking");
            speaking.transform.parent = canvas.transform;
            speaking.transform.localPosition = new Vector3(3.25f, -0.9f, 0.2f);
            speaking.transform.localRotation = Quaternion.Euler(-90, -90, 0);
            
            var speakingImage = speaking.AddComponent<Image>();
            speakingImage.sprite = Assets.GetResource<Sprite>("SpeakingSymbol");;
            speakingImage.color = Color.black;
            
            speaking.transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);
            
            var arrowUp = new GameObject("ArrowUp");
            arrowUp.transform.parent = canvas.transform;
            arrowUp.transform.localPosition = new Vector3(3.25f, -0.9f, -0.2f);
            arrowUp.transform.localRotation = Quaternion.Euler(-90, -90, 0);
            arrowUp.transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);

            var arrowUpImage = arrowUp.AddComponent<Image>();
            arrowUpImage.sprite = Assets.GetResource<Sprite>("arrow2");;
            arrowUpImage.color = Color.black;
            
            var arrowDown = new GameObject("ArrowDown");
            arrowDown.transform.parent = canvas.transform;
            arrowDown.transform.localPosition = new Vector3(3.25f, -0.9f, -0.1f);
            arrowDown.transform.localRotation = Quaternion.Euler(-90, -90, 0);
            arrowDown.transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);
            
            var arrowDownImage = arrowDown.AddComponent<Image>();
            arrowDownImage.sprite = Assets.GetResource<Sprite>("arrow2");;
            arrowUpImage.transform.Rotate(0, 0, 180);
            arrowDownImage.color = Color.black;
            
            
            var textComponent = text.AddComponent<Text>();
            textComponent.text = $"<b><size=40>{FrequencyWalkie.frequencies[FrequencyWalkie.walkieTalkieFrequencies[__instance.GetInstanceID()]]}</size><i><size=30>MHz</size></i></b>";
            textComponent.font = Assets.GetResource<Font>("3270-Regular");;
            textComponent.color = Color.black;
            textComponent.fontSize = 40;
            textComponent.lineSpacing = 0.5f;
            
            text.transform.localScale = new Vector3(0.015f, 0.01f, 0.01f);
            
            canvas.enabled = false;
        }
        
        private static void WalkieTalkie_SwitchWalkieTalkieOn(WalkieTalkie __instance)
        {
            __instance.gameObject.GetComponent<Canvas>().enabled = __instance.isBeingUsed;
        }

        private static void WalkieTalkie_Update(WalkieTalkie __instance)
        {
            var player = GameNetworkManager.Instance.localPlayerController;
            var isCurrentItemWalkie = player.ItemSlots[player.currentItemSlot] == __instance;
            // player has to be holding the walkie talkie 
            // and walkie talkie has to be turned on
            if (!__instance.isBeingUsed || !isCurrentItemWalkie) return;
            
            MethodInfo SendWalkieTalkieStartTransmissionSFX = AccessTools.Method(typeof(WalkieTalkie), "SendWalkieTalkieStartTransmissionSFX");
            if (UnityInput.Current.GetKeyUp(KeyCode.F))
            {
                FrequencyWalkie.walkieTalkieFrequencies[__instance.GetInstanceID()]--;
                if (FrequencyWalkie.walkieTalkieFrequencies[__instance.GetInstanceID()] < 0)
                {
                    FrequencyWalkie.walkieTalkieFrequencies[__instance.GetInstanceID()] = FrequencyWalkie.frequencies.Count - 1;
                }
                
                SendWalkieTalkieStartTransmissionSFX.Invoke(__instance, new object[] {(int)__instance.playerHeldBy.playerClientId});
                __instance.StartCoroutine(FrequencyWalkie.OnFrequencyChanged(__instance, false));
            } else if (UnityInput.Current.GetKeyUp(KeyCode.R))
            {
                FrequencyWalkie.walkieTalkieFrequencies[__instance.GetInstanceID()] = (FrequencyWalkie.walkieTalkieFrequencies[__instance.GetInstanceID()] + 1) % FrequencyWalkie.frequencies.Count;
                
                SendWalkieTalkieStartTransmissionSFX.Invoke(__instance, new object[] {(int)__instance.playerHeldBy.playerClientId});
                __instance.StartCoroutine(FrequencyWalkie.OnFrequencyChanged(__instance, true));
            }
        }
        
        private static bool WalkieTalkie_SetLocalClientSpeaking(WalkieTalkie __instance, PlayerControllerB ___previousPlayerHeldBy, bool speaking)
        {
            if (___previousPlayerHeldBy.speakingToWalkieTalkie != speaking)
            {
                ___previousPlayerHeldBy.speakingToWalkieTalkie = speaking;
                if (speaking)
                {
                    FrequencyWalkie.SetPlayerSpeakingOnWalkieTalkieServerRpc(__instance, (int)___previousPlayerHeldBy.playerClientId, FrequencyWalkie.walkieTalkieFrequencies[__instance.GetInstanceID()]);
                    return false;
                }
                __instance.UnsetPlayerSpeakingOnWalkieTalkieServerRpc((int)___previousPlayerHeldBy.playerClientId);
            }

            return false;
        }
        
        // NOTE: need to patch this
        private static bool WalkieTalkie_HandlePlayerSpeakingServerRPC(
            NetworkBehaviour target,
            FastBufferReader reader,
            __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if (networkManager == null || !networkManager.IsListening)
                return false;
            if ((long) rpcParams.Server.Receive.SenderClientId != (long) target.OwnerClientId)
            {
                if (networkManager.LogLevel > LogLevel.Normal)
                    return false;
                Debug.LogError((object) "Only the owner can invoke a ServerRpc that requires ownership!");
            }
            else
            {
                ByteUnpacker.ReadValueBitPacked(reader, out int playerId);
                ByteUnpacker.ReadValueBitPacked(reader, out int frequency);
                AccessTools.Field(typeof(WalkieTalkie), "__rpc_exec_stage").SetValue(target, (int)RpcExecStage.Server);
                FrequencyWalkie.SetPlayerSpeakingOnWalkieTalkieServerRpc((WalkieTalkie)target, playerId, frequency);
                AccessTools.Field(typeof(WalkieTalkie), "__rpc_exec_stage").SetValue(target, (int)RpcExecStage.None);
            }

            return false;
        }
        
        // NOTE: need to patch this
        private static bool WalkieTalkie_HandlePlayerSpeakingClientRPC(
            NetworkBehaviour target,
            FastBufferReader reader,
            __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if (networkManager == null || !networkManager.IsListening)
                return false;
            ByteUnpacker.ReadValueBitPacked(reader, out int playerId);
            ByteUnpacker.ReadValueBitPacked(reader, out int frequency);
            AccessTools.Field(typeof(WalkieTalkie), "__rpc_exec_stage").SetValue(target, (int)RpcExecStage.Client);
            FrequencyWalkie.SetPlayerSpeakingOnWalkieTalkieClientRpc((WalkieTalkie)target, playerId, frequency);
            AccessTools.Field(typeof(WalkieTalkie), "__rpc_exec_stage").SetValue(target, (int)RpcExecStage.None);

            return false;
        }
    }
}