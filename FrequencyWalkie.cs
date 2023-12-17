using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FrequencyWalkie
{
    enum RpcExecStage
    {
        None,
        Server,
        Client,
    }
    
    [BepInPlugin("larko.frequencywalkie", "FrequencyWalkie", "1.0.0")]
    public class FrequencyWalkie : BaseUnityPlugin
    {
        private delegate FastBufferWriter beginSendServerRpcDelegate(
            uint rpcMethodId,
            ServerRpcParams serverRpcParams,
            RpcDelivery rpcDelivery);
        private delegate void endSendServerRpcDelegate(
            ref FastBufferWriter bufferWriter,
            uint rpcMethodId,
            ServerRpcParams serverRpcParams,
            RpcDelivery rpcDelivery);
        
        private delegate FastBufferWriter beginSendClientRpcDelegate(
            uint rpcMethodId,
            ClientRpcParams clientRpcParams,
            RpcDelivery rpcDelivery);
        private delegate void endSendClientRpcDelegate(
            ref FastBufferWriter bufferWriter,
            uint rpcMethodId,
            ClientRpcParams serverRpcParams,
            RpcDelivery rpcDelivery);

        private delegate void SendWalkieTalkieStartTransmissionSFXDelegate(int playerId);
        
        private static int frequencyIndex = 0;
        private static List<string> _frequencyList = new List<string> {"BR.d", "18.3", "19.5", "21.7", "23.3", "25.0", "27.9", "29.3", "31.3", "33.6", "35.4", "36.8", "37.7", "38.6", "39.4",
            "40.2", "42.3", "43.8", "44.5", "45.1", "46.9", "47.2", "47.8", "48.1", "48.5"};
        
        private static Font font;
        private static Sprite speakerSprite;
        private static Sprite arrowUpSprite;
        private static Sprite circleSprite;
        
        void Awake()
        {
            Harmony harmony = new Harmony("larko.frequencywalkie");
            MethodInfo originalSetLocalClientSpeaking = AccessTools.Method(typeof(WalkieTalkie), "SetLocalClientSpeaking");
            MethodInfo patchSetLocalClientSpeaking = AccessTools.Method(typeof(FrequencyWalkie), "WalkieTalkie_SetLocalClientSpeaking");
            harmony.Patch(originalSetLocalClientSpeaking, new HarmonyMethod(patchSetLocalClientSpeaking));
            
            MethodInfo original__rpc_handler_64994802 = AccessTools.Method(typeof(WalkieTalkie), "__rpc_handler_64994802");
            MethodInfo patch__rpc_handler_64994802 = AccessTools.Method(typeof(FrequencyWalkie), "WalkieTalkie_HandlePlayerSpeakingServerRPC");
            harmony.Patch(original__rpc_handler_64994802, new HarmonyMethod(patch__rpc_handler_64994802));
            
            MethodInfo original__rpc_handler_2961867446 = AccessTools.Method(typeof(WalkieTalkie), "__rpc_handler_2961867446");
            MethodInfo patch__rpc_handler_2961867446 = AccessTools.Method(typeof(FrequencyWalkie), "WalkieTalkie_HandlePlayerSpeakingClientRPC");
            harmony.Patch(original__rpc_handler_2961867446, new HarmonyMethod(patch__rpc_handler_2961867446));
            
            MethodInfo originUpdate = AccessTools.Method(typeof(WalkieTalkie), "Update");
            MethodInfo patchUpdate = AccessTools.Method(typeof(FrequencyWalkie), "WalkieTalkie_Update");
            harmony.Patch(originUpdate, null, new HarmonyMethod(patchUpdate));
            
            MethodInfo originStart = AccessTools.Method(typeof(WalkieTalkie), "Start");
            MethodInfo patchStart = AccessTools.Method(typeof(FrequencyWalkie), "WalkieTalkie_Start");
            harmony.Patch(originStart, null, new HarmonyMethod(patchStart));
            
            MethodInfo originSwitchWalkieTalkieOn = AccessTools.Method(typeof(WalkieTalkie), "SwitchWalkieTalkieOn");
            MethodInfo patchSwitchWalkieTalkieOn = AccessTools.Method(typeof(FrequencyWalkie), "WalkieTalkie_SwitchWalkieTalkieOn");
            harmony.Patch(originSwitchWalkieTalkieOn, null, new HarmonyMethod(patchSwitchWalkieTalkieOn));
            
            // TODO: Patch walkie talkie equip func to update frequency to up to date one
            // or store frequencies by walkie talkie instances.
        }


        public static void WalkieTalkie_Start(WalkieTalkie __instance)
        {
            // add control tip to the walkie talkie
            __instance.itemProperties.toolTips =
                __instance.itemProperties.toolTips.AddToArray("[R] Increase frequency\n[F] Decrease frequency");
            
            // load font
            if (font == null)
            {
                font = GetRessource<Font>("3270-Regular");
            }
            
            // load sprites
            if (speakerSprite == null)
            {
                speakerSprite = GetRessource<Sprite>("SpeakingSymbol");
            }
            
            if (arrowUpSprite == null)
            {
                arrowUpSprite = GetRessource<Sprite>("arrow2");
            }
            
            if (circleSprite == null)
            {
                circleSprite = GetRessource<Sprite>("scanCircle2");
            }
            
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
            circleImage.sprite = circleSprite;
            circleImage.color = Color.black;
            
            var speaking = new GameObject("Speaking");
            speaking.transform.parent = canvas.transform;
            speaking.transform.localPosition = new Vector3(3.25f, -0.9f, 0.2f);
            speaking.transform.localRotation = Quaternion.Euler(-90, -90, 0);
            
            var speakingImage = speaking.AddComponent<Image>();
            speakingImage.sprite = speakerSprite;
            speakingImage.color = Color.black;
            
            speaking.transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);
            
            var arrowUp = new GameObject("ArrowUp");
            arrowUp.transform.parent = canvas.transform;
            arrowUp.transform.localPosition = new Vector3(3.25f, -0.9f, -0.2f);
            arrowUp.transform.localRotation = Quaternion.Euler(-90, -90, 0);
            arrowUp.transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);

            var arrowUpImage = arrowUp.AddComponent<Image>();
            arrowUpImage.sprite = arrowUpSprite;
            arrowUpImage.color = Color.black;
            
            var arrowDown = new GameObject("ArrowDown");
            arrowDown.transform.parent = canvas.transform;
            arrowDown.transform.localPosition = new Vector3(3.25f, -0.9f, -0.1f);
            arrowDown.transform.localRotation = Quaternion.Euler(-90, -90, 0);
            arrowDown.transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);
            
            var arrowDownImage = arrowDown.AddComponent<Image>();
            arrowDownImage.sprite = arrowUpSprite;
            arrowUpImage.transform.Rotate(0, 0, 180);
            arrowDownImage.color = Color.black;
            
            
            var textComponent = text.AddComponent<Text>();
            textComponent.text = $"<b><size=40>{_frequencyList[frequencyIndex]}</size><i><size=30>MHz</size></i></b>";
            textComponent.font = font;
            textComponent.color = Color.black;
            textComponent.fontSize = 40;
            textComponent.lineSpacing = 0.5f;
            
            text.transform.localScale = new Vector3(0.015f, 0.01f, 0.01f);
            
            canvas.enabled = false;
        }
        
        private static T GetRessource<T>(string objName) where T : Object
        {
            T[] objects = Resources.FindObjectsOfTypeAll<T>();
            foreach (T obj in objects)
            {
                if (obj.name == objName)
                {
                    return obj;
                }
            }
            return null;
        }
        
        private static IEnumerator OnFrequencyChanged(WalkieTalkie walkie, bool increased)
        {
            var canvas = walkie.gameObject.GetComponent<Canvas>();

            var text = canvas.GetComponentInChildren<Text>();
            text.text = $"<b><size=40>{_frequencyList[frequencyIndex]}</size><i><size=30>MHz</size></i></b>";
            
            // we show the broadcast icon if frequency is 0 (broad)
            if (frequencyIndex == 0)
            {
                canvas.transform.GetChild(canvas.transform.childCount - 4).gameObject.SetActive(true);
            }
            else
            {
                canvas.transform.GetChild(canvas.transform.childCount - 4).gameObject.SetActive(false);
            }
            
            if (increased)
                canvas.transform.GetChild(canvas.transform.childCount - 2).gameObject.SetActive(false);
            else
                canvas.transform.GetChild(canvas.transform.childCount - 1).gameObject.SetActive(false);
            yield return new WaitForSeconds(0.5f);
            canvas.transform.GetChild(canvas.transform.childCount - 1).gameObject.SetActive(true);            
            canvas.transform.GetChild(canvas.transform.childCount - 2).gameObject.SetActive(true);
        }
        
        public static void WalkieTalkie_SwitchWalkieTalkieOn(WalkieTalkie __instance)
        {
            __instance.gameObject.GetComponent<Canvas>().enabled = __instance.isBeingUsed;
        }

        public static void WalkieTalkie_Update(WalkieTalkie __instance)
        {
            var player = GameNetworkManager.Instance.localPlayerController;
            var isCurrentItemWalkie = player.ItemSlots[player.currentItemSlot] == __instance;
            // player has to be holding the walkie talkie 
            // and walkie talkie has to be turned on
            if (!__instance.isBeingUsed || !isCurrentItemWalkie) return;
            
            MethodInfo SendWalkieTalkieStartTransmissionSFX = AccessTools.Method(typeof(WalkieTalkie), "SendWalkieTalkieStartTransmissionSFX");
            if (UnityInput.Current.GetKeyUp(KeyCode.F))
            {
                frequencyIndex--;
                if (frequencyIndex < 0)
                {
                    frequencyIndex = _frequencyList.Count - 1;
                }
                
                SendWalkieTalkieStartTransmissionSFX.Invoke(__instance, new object[] {(int)__instance.playerHeldBy.playerClientId});
                __instance.StartCoroutine(OnFrequencyChanged(__instance, false));
            } else if (UnityInput.Current.GetKeyUp(KeyCode.R))
            {
                frequencyIndex = (frequencyIndex + 1) % _frequencyList.Count;
                
                SendWalkieTalkieStartTransmissionSFX.Invoke(__instance, new object[] {(int)__instance.playerHeldBy.playerClientId});
                __instance.StartCoroutine(OnFrequencyChanged(__instance, true));
            }
        }
        
        public static bool WalkieTalkie_SetLocalClientSpeaking(WalkieTalkie __instance, PlayerControllerB ___previousPlayerHeldBy, bool speaking)
        {
            if (___previousPlayerHeldBy.speakingToWalkieTalkie != speaking)
            {
                ___previousPlayerHeldBy.speakingToWalkieTalkie = speaking;
                if (speaking)
                {
                    WalkieTalkie_SetPlayerSpeakingOnWalkieTalkieServerRpc(__instance, (int)___previousPlayerHeldBy.playerClientId, frequencyIndex);
                    return false;
                }
                __instance.UnsetPlayerSpeakingOnWalkieTalkieServerRpc((int)___previousPlayerHeldBy.playerClientId);
            }

            return false;
        }

        public static void WalkieTalkie_SetPlayerSpeakingOnWalkieTalkieServerRpc(WalkieTalkie instance, int playerId, int frequency)
        {
            NetworkManager networkManager = instance.NetworkManager;
            if (networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            
            var rpc_exec_stage = (int)AccessTools.Field(typeof(WalkieTalkie), "__rpc_exec_stage").GetValue(instance);
            MethodInfo beginSendServerRpc = AccessTools.Method(typeof(WalkieTalkie), "__beginSendServerRpc"); 
            MethodInfo endSendServerRpc = AccessTools.Method(typeof(WalkieTalkie), "__endSendServerRpc");
                
            if (rpc_exec_stage != (int)RpcExecStage.Server && (networkManager.IsClient || networkManager.IsHost))
            {
                if (instance.OwnerClientId != networkManager.LocalClientId)
                {
                    if (networkManager.LogLevel <= LogLevel.Normal)
                    {
                        Debug.LogError("Only the owner can invoke a ServerRpc that requires ownership!");
                    }
                    return;
                }
                ServerRpcParams serverRpcParams = default;
                FastBufferWriter writer = (FastBufferWriter)beginSendServerRpc.Invoke(instance, new object[] {64994802U, serverRpcParams, RpcDelivery.Reliable});
                BytePacker.WriteValueBitPacked(writer, playerId);
                BytePacker.WriteValueBitPacked(writer, frequency);
                // maybe ref bug - writer will get cloned?
                endSendServerRpc.Invoke(instance, new object[] {writer, 64994802U, serverRpcParams, RpcDelivery.Reliable});
            }
            if (rpc_exec_stage != (int)RpcExecStage.Server || (!networkManager.IsServer && !networkManager.IsHost))
            {
                return;
            }
            WalkieTalkie_SetPlayerSpeakingOnWalkieTalkieClientRpc(instance, playerId, frequency);
        }
        
        public static void WalkieTalkie_SetPlayerSpeakingOnWalkieTalkieClientRpc(WalkieTalkie instance, int playerId, int frequency)
        {
            NetworkManager networkManager = instance.NetworkManager;
            if (networkManager == null || !networkManager.IsListening)
                return;
            
            var rpc_exec_stage = (int)AccessTools.Field(typeof(WalkieTalkie), "__rpc_exec_stage").GetValue(instance);
            MethodInfo beginSendClientRpc = AccessTools.Method(typeof(WalkieTalkie), "__beginSendClientRpc");
            MethodInfo endSendClientRpc = AccessTools.Method(typeof(WalkieTalkie), "__endSendClientRpc");
            MethodInfo SendWalkieTalkieStartTransmissionSFX = AccessTools.Method(typeof(WalkieTalkie), "SendWalkieTalkieStartTransmissionSFX");
            
            if (rpc_exec_stage != (int)RpcExecStage.Client && (networkManager.IsServer || networkManager.IsHost))
            {
                ClientRpcParams clientRpcParams = default;
                FastBufferWriter bufferWriter = (FastBufferWriter) beginSendClientRpc.Invoke(instance, new object[] {2961867446U, clientRpcParams, RpcDelivery.Reliable});
                BytePacker.WriteValueBitPacked(bufferWriter, playerId);
                BytePacker.WriteValueBitPacked(bufferWriter, frequency);
                // maybe ref bug - writer will get cloned?
                endSendClientRpc.Invoke(instance, new object[] {bufferWriter, 2961867446U, clientRpcParams, RpcDelivery.Reliable});
            }
            if (rpc_exec_stage != (int)RpcExecStage.Client || !networkManager.IsClient && !networkManager.IsHost)
                return;

            if (frequencyIndex != frequency && frequency != 0)
            {
                return;
            }
            StartOfRound.Instance.allPlayerScripts[playerId].speakingToWalkieTalkie = true;
            instance.clientIsHoldingAndSpeakingIntoThis = true;
            SendWalkieTalkieStartTransmissionSFX.Invoke(instance, new object[] {playerId});
            StartOfRound.Instance.UpdatePlayerVoiceEffects();
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
                WalkieTalkie_SetPlayerSpeakingOnWalkieTalkieServerRpc((WalkieTalkie)target, playerId, frequency);
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
            WalkieTalkie_SetPlayerSpeakingOnWalkieTalkieClientRpc((WalkieTalkie)target, playerId, frequency);
            AccessTools.Field(typeof(WalkieTalkie), "__rpc_exec_stage").SetValue(target, (int)RpcExecStage.None);

            return false;
        }
    }
}