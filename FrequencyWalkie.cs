using System;
using System.Reflection;
using System.Text;
using BepInEx;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace FrequencyWalkie
{
    public enum RpcExecStage
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
        
        private static int s_Frequency = 0;
        
        void Awake()
        {
            Harmony harmony = new Harmony("larko.frequencywalkie");
                
            MethodInfo originalSetLocalClientSpeaking = AccessTools.Method(typeof(WalkieTalkie), "SetLocalClientSpeaking");
            MethodInfo patchSetLocalClientSpeaking = AccessTools.Method(typeof(FrequencyWalkie), "SetLocalClientSpeaking");
            harmony.Patch(originalSetLocalClientSpeaking, new HarmonyMethod(patchSetLocalClientSpeaking));
            
            MethodInfo original__rpc_handler_64994802 = AccessTools.Method(typeof(WalkieTalkie), "__rpc_handler_64994802");
            MethodInfo patch__rpc_handler_64994802 = AccessTools.Method(typeof(FrequencyWalkie), "__rpc_handler_64994802");
            harmony.Patch(original__rpc_handler_64994802, new HarmonyMethod(patch__rpc_handler_64994802));
            
            MethodInfo original__rpc_handler_2961867446 = AccessTools.Method(typeof(WalkieTalkie), "__rpc_handler_2961867446");
            MethodInfo patch__rpc_handler_2961867446 = AccessTools.Method(typeof(FrequencyWalkie), "__rpc_handler_2961867446");
            harmony.Patch(original__rpc_handler_2961867446, new HarmonyMethod(patch__rpc_handler_2961867446));
            
            MethodInfo originUpdate = AccessTools.Method(typeof(WalkieTalkie), "Update");
            MethodInfo patchUpdate = AccessTools.Method(typeof(FrequencyWalkie), "Update");
            harmony.Patch(originUpdate, null, new HarmonyMethod(patchUpdate));
        }

        public static void Update(WalkieTalkie __instance)
        {
            PlayerControllerB previousPlayerHeldBy = (PlayerControllerB)AccessTools.Field(typeof(WalkieTalkie), "previousPlayerHeldBy").GetValue(__instance);

            if (!__instance.isBeingUsed || __instance.playerHeldBy.playerClientId != GameNetworkManager.Instance.localPlayerController.playerClientId) return; 
            
            MethodInfo SendWalkieTalkieStartTransmissionSFX = AccessTools.Method(typeof(WalkieTalkie), "SendWalkieTalkieStartTransmissionSFX");
            MethodInfo SendWalkieTalkieEndTransmissionSFX = AccessTools.Method(typeof(WalkieTalkie), "SendWalkieTalkieEndTransmissionSFX");

            if (UnityInput.Current.GetKeyUp(KeyCode.E))
            {
                s_Frequency--;
                if (s_Frequency < 0)
                {
                    s_Frequency = 100;
                }
                
                SendWalkieTalkieEndTransmissionSFX.Invoke(__instance, new object[] {(int)__instance.playerHeldBy.playerClientId});
                HUDManager.Instance.DisplayTip("Walkie Talkie", "Frequency set to " + s_Frequency + ".");
            } else if (UnityInput.Current.GetKeyUp(KeyCode.R))
            {
                s_Frequency++;
                if (s_Frequency > 100)
                {
                    s_Frequency = 0;
                }
                
                SendWalkieTalkieStartTransmissionSFX.Invoke(__instance, new object[] {(int)__instance.playerHeldBy.playerClientId});
                HUDManager.Instance.DisplayTip("Walkie Talkie", "Frequency set to " + s_Frequency + ".");
            }
            
        }

        public static bool SetLocalClientSpeaking(WalkieTalkie __instance, PlayerControllerB ___previousPlayerHeldBy, bool speaking)
        {
            if (___previousPlayerHeldBy.speakingToWalkieTalkie != speaking)
            {
                ___previousPlayerHeldBy.speakingToWalkieTalkie = speaking;
                if (speaking)
                {
                    SetPlayerSpeakingOnWalkieTalkieServerRpc(__instance, (int)___previousPlayerHeldBy.playerClientId, s_Frequency);
                    return false;
                }
                __instance.UnsetPlayerSpeakingOnWalkieTalkieServerRpc((int)___previousPlayerHeldBy.playerClientId);
            }

            return false;
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

            if (s_Frequency != frequency)
            {
                return;
            }
            StartOfRound.Instance.allPlayerScripts[playerId].speakingToWalkieTalkie = true;
            instance.clientIsHoldingAndSpeakingIntoThis = true;
            SendWalkieTalkieStartTransmissionSFX.Invoke(instance, new object[] {playerId});
            StartOfRound.Instance.UpdatePlayerVoiceEffects();
        }
        
        // NOTE: need to patch this
        private static bool __rpc_handler_64994802(
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
                SetPlayerSpeakingOnWalkieTalkieServerRpc((WalkieTalkie)target, playerId, frequency);
                AccessTools.Field(typeof(WalkieTalkie), "__rpc_exec_stage").SetValue(target, (int)RpcExecStage.None);
            }

            return false;
        }
        
        // NOTE: need to patch this
        private static bool __rpc_handler_2961867446(
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
            SetPlayerSpeakingOnWalkieTalkieClientRpc((WalkieTalkie)target, playerId, frequency);
            AccessTools.Field(typeof(WalkieTalkie), "__rpc_exec_stage").SetValue(target, (int)RpcExecStage.None);

            return false;
        }
    }
}