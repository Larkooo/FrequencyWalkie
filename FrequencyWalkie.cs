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
    
    [BepInPlugin("larko.frequencywalkie", "FrequencyWalkie", "1.2.0")]
    public class FrequencyWalkie : BaseUnityPlugin
    {
        // A hash map of all walkie talkies and their selected frequency
        // the key is the walkie talkie's gameobject instance ID 
        // the value is the frequency index
        public static Dictionary<int, int> walkieTalkieFrequencies = new Dictionary<int, int>();
        public static List<string> frequencies = new List<string> {"BR.d", "18.3", "19.5", "21.7", "23.3", "25.0", "27.9", "29.3", "31.3", "33.6", "35.4", "36.8", "37.7", "38.6", "39.4",
            "40.2", "42.3", "43.8", "44.5", "45.1", "46.9", "47.2", "47.8", "48.1", "48.5"};

        public static string tooltip = "[R] Increase frequency\n[F] Decrease frequency";
        
        void Awake()
        {
            Harmony harmony = new Harmony("larko.frequencywalkie");
            Patches.ApplyPatches(harmony);
        }
        
        
        public static T GetResource<T>(string objName) where T : Object
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
        
        public static IEnumerator OnFrequencyChanged(WalkieTalkie walkie, bool increased)
        {
            var canvas = walkie.gameObject.GetComponent<Canvas>();

            var text = canvas.GetComponentInChildren<Text>();
            text.text = $"<b><size=40>{frequencies[walkieTalkieFrequencies[walkie.GetInstanceID()]]}</size><i><size=30>MHz</size></i></b>";
            
            MethodInfo SendWalkieTalkieStartTransmissionSFX = AccessTools.Method(typeof(WalkieTalkie), "SendWalkieTalkieStartTransmissionSFX");
            SendWalkieTalkieStartTransmissionSFX.Invoke(walkie, new object[] {(int)walkie.playerHeldBy.playerClientId});
            
            // we show the broadcast icon if frequency is 0 (broad)
            if (walkieTalkieFrequencies[walkie.GetInstanceID()] == 0)
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
        
        public static void SetPlayerSpeakingOnWalkieTalkieServerRpc(WalkieTalkie instance, int playerId, int frequency)
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
            SetPlayerSpeakingOnWalkieTalkieClientRpc(instance, playerId, frequency);
        }
        
        public static void SetPlayerSpeakingOnWalkieTalkieClientRpc(WalkieTalkie instance, int playerId, int frequency)
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

            // update the frequency on the incoming walkie talkie
            for (int i = 0; i < WalkieTalkie.allWalkieTalkies.Count; i++)
            {
                if ((int)WalkieTalkie.allWalkieTalkies[i].playerHeldBy.playerClientId == playerId)
                {
                    walkieTalkieFrequencies[WalkieTalkie.allWalkieTalkies[i].GetInstanceID()] = frequency;
                    // update text
                    instance.StartCoroutine(OnFrequencyChanged(WalkieTalkie.allWalkieTalkies[i], false));
                    break;
                }
            }
            
            if (walkieTalkieFrequencies[instance.GetInstanceID()] != frequency && frequency != 0)
                return;
            
            
            StartOfRound.Instance.allPlayerScripts[playerId].speakingToWalkieTalkie = true;
            instance.clientIsHoldingAndSpeakingIntoThis = true;
            SendWalkieTalkieStartTransmissionSFX.Invoke(instance, new object[] {playerId});
            StartOfRound.Instance.UpdatePlayerVoiceEffects();
        }
    }
}