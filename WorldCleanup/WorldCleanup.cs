﻿using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;
using UIExpansionKit.API;
using System;
using System.Reflection;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using VRC.Core;
using VRC.Playables;
using UnhollowerRuntimeLib;

namespace WorldCleanup
{
    public class WorldCleanupMod : MelonMod
    {
        private static Dictionary<string, GameObject> s_PlayerList;

        private string[] DefaultParameterNames = new string[]
        {
            "Viseme",
            "GestureLeft",
            "GestureLeftWeight",
            "GestureRight",
            "GestureRightWeight",
            "TrackingType",
            "VRMode",
            "MuteSelf",
            "Grounded",
            "AngularY",
            "Upright",
            "AFK",
            "Seated",
            "InStation",
            "VelocityX",
            "VelocityY",
            "VelocityZ",
            "IsLocal",
            "AvatarVersion",
            "VRCEmote",
            "VRCFaceBlendH",
            "VRCFaceBlendV",
        };

        private static void WitchHunt(GameObject avatar)
        {
            foreach (var renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).AsEnumerable())
            {
                foreach (var material in renderer.materials)
                {
                    if (material.HasProperty("_OrificeData"))
                    {
                        MelonLogger.Msg(ConsoleColor.Red, "COOMER DETECTED");
                    }
                }
            }
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            return;
            /* Get active scene */
            Scene active_scene = SceneManager.GetActiveScene();

            /* Iterate root objects */
            foreach (var sceneObject in active_scene.GetRootGameObjects().ToArray<GameObject>())
            {
                /* Disable all light shadows */
                foreach (var light in sceneObject.GetComponentsInChildren<Light>())
                {
                    MelonLogger.Msg(ConsoleColor.Yellow, $"{light.transform.name}: disabling light shadows");
                    light.shadows = LightShadows.None;
                }

                /* Other? */
            }
        }

        public override void VRChat_OnUiManagerInit()
        {
            /* Initialize global asset loader */
            Assets.Initialize();

            /* Load our custom UI elements */
            UiExpansion.LoadUiObjects();

            /* Hook into "OnAvatarInstantiated" */
            /* Note: Failure is an unrecoverable error */
            unsafe
            {
                var intPtr = (IntPtr)typeof(VRCAvatarManager.MulticastDelegateNPublicSealedVoGaVRBoUnique)
                    .GetField("NativeMethodInfoPtr_Invoke_Public_Virtual_New_Void_GameObject_VRC_AvatarDescriptor_Boolean_0",
                        BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                MelonUtils.NativeHookAttach(intPtr, new Action<IntPtr, IntPtr, IntPtr, bool>(OnAvatarInstantiated).Method.MethodHandle.GetFunctionPointer());
                _onAvatarInstantiatedDelegate = Marshal.GetDelegateForFunctionPointer<AvatarInstantiatedDelegate>(*(IntPtr*)(void*)intPtr);
            }

            s_PlayerList = new Dictionary<string, GameObject>();

            ExpansionKitApi.GetExpandedMenu(ExpandedMenu.QuickMenu).AddSimpleButton("Player List", PlayerList);
            // ExpansionKitApi.GetExpandedMenu(ExpandedMenu.QuickMenu).AddSimpleButton("WorldCleanup", WorldCleanupMenu);
            ExpansionKitApi.GetExpandedMenu(ExpandedMenu.UserQuickMenu).AddSimpleButton("Avatar Toggles", OnUserQuickMenu);

            MelonLogger.Msg("WorldCleanup ready!");
        }

        private void WorldCleanupMenu()
        {
            var settingsMenu = ExpansionKitApi.CreateCustomQuickMenuPage(LayoutDescription.QuickMenu3Columns);
            settingsMenu.AddLabel("\n\n  World Cleanup");
            settingsMenu.AddSimpleButton("Player List", PlayerList);

            settingsMenu.Show();
        }

        private void OnUserQuickMenu()
        {
            var player = QuickMenu.prop_QuickMenu_0.field_Private_Player_0?.prop_VRCPlayer_0;
            if (player == null)
                return;

            AvatarList(player.prop_String_0, true);
        }

        private void PlayerList()
        {
            /* Filter inactive avatar objects */
            s_PlayerList = s_PlayerList.Where(o => o.Value).ToDictionary(o => o.Key, o => o.Value);

            var playerList = ExpansionKitApi.CreateCustomQuickMenuPage(LayoutDescription.WideSlimList);
            foreach (var entry in s_PlayerList)
                playerList.AddSimpleButton(entry.Key, () => { AvatarList(entry.Key, false); });

            playerList.AddSimpleButton("Back", () => { playerList.Hide(); /* WorldCleanupMenu(); */ });
            playerList.Show();
        }

        private void AvatarList(string player_name, bool close_on_exit)
        {
            var avatar = s_PlayerList[player_name];
            if (!avatar)
                return;

            var manager = avatar.transform.GetComponentInParent<VRCAvatarManager>();
            /* var is_self = manager.field_Private_VRCPlayer_0 == VRCPlayer.field_Internal_Static_VRCPlayer_0;
            var mirrored = manager.field_Private_GameObject_0; */

            var avatarList = ExpansionKitApi.CreateCustomQuickMenuPage(LayoutDescription.WideSlimList);
            {
                /* Animator Toggle */
                var animator = avatar.GetComponent<Animator>();
                UiExpansion.AddToggleListItem(avatarList, "Animator", (state) => { animator.enabled = state; }, () => { return animator.enabled; });
            }
            {
                /* Renderer Toggle */
                var renderers = avatar.transform.GetComponentsInChildren<Renderer>(true);

                void ShowRendererToggleList(string type, IEnumerable<Renderer> list)
                {
                    var meshList = ExpansionKitApi.CreateCustomQuickMenuPage(LayoutDescription.WideSlimList);
                    if (type != null)
                        meshList.AddLabel(type);
                    foreach (var mesh in list)
                    {
                        UiExpansion.AddToggleListItem(meshList, type != null ? mesh.gameObject.name : $"{mesh.GetIl2CppType().Name}: {mesh.gameObject.name}",
                            (state) => {
                                mesh.enabled = state;
                                mesh.gameObject.active = state;
                            },
                            () => {
                                return mesh.enabled && mesh.gameObject.active;
                            }
                        );
                    }
                    meshList.AddSimpleButton("Back", () => { meshList.Hide(); AvatarList(player_name, close_on_exit); });
                    meshList.Show();
                }

                /* Get Skinned Mesh Renderers */
                var smr = renderers.Where(o => { return o.TryCast<SkinnedMeshRenderer>(); });
                if (smr.Count() > 0)
                {
                    avatarList.AddSimpleButton($"SkinnedMeshRenderer: {smr.Count()}", () => {
                        var meshList = ExpansionKitApi.CreateCustomQuickMenuPage(LayoutDescription.WideSlimList);
                        meshList.AddLabel("SkinnedMeshRenderers");
                        foreach (var mesh in smr)
                        {
                            void on_click(bool state) { mesh.gameObject.active = mesh.enabled = state; }
                            bool get_initial_state() { return mesh.enabled && mesh.gameObject.active; }

                            var renderer = mesh.Cast<SkinnedMeshRenderer>();
                            var count = renderer.sharedMesh.blendShapeCount;
                            if (count > 0)
                            {
                                UiExpansion.AddButtonToggleListItem(
                                    meshList,
                                    mesh.gameObject.name,
                                    $"Blendshapes: {count}",
                                    () => { BlendShapeList(renderer, player_name, close_on_exit); },
                                    on_click,
                                    get_initial_state
                                );
                            }
                            else
                            {
                                UiExpansion.AddToggleListItem(meshList, mesh.gameObject.name, on_click, get_initial_state);
                            }
                        }
                        meshList.AddSimpleButton("Back", () => { meshList.Hide(); AvatarList(player_name, close_on_exit); });
                        meshList.Show();
                    });
                }

                /* Get Mesh Renderers */
                var mr = renderers.Where(o => { return o.TryCast<MeshRenderer>(); });
                if (mr.Count() > 0)
                    avatarList.AddSimpleButton($"MeshRenderer: {mr.Count()}", () => { ShowRendererToggleList("MeshRenderer", mr); });

                /* Get Particle System Renderers */
                var pr = renderers.Where(o => { return o.TryCast<ParticleSystemRenderer>(); });
                if (pr.Count() > 0)
                    avatarList.AddSimpleButton($"ParticleSystemRenderer: {pr.Count()}", () => { ShowRendererToggleList("ParticleSystemRenderer", pr); });

                /* Other renderers */
                var remainder = renderers.Where(o => { return !o.TryCast<SkinnedMeshRenderer>() && !o.TryCast<MeshRenderer>() && !o.TryCast<ParticleSystemRenderer>(); });
                if (remainder.Count() > 0)
                    avatarList.AddSimpleButton($"Other: {remainder.Count()}", () => { ShowRendererToggleList(null, remainder); });
            }
            {
                /* Parameters */
                var parameters = manager.field_Private_AvatarPlayableController_0?
                                       .field_Private_Dictionary_2_Int32_AvatarParameter_0
                                       .Values;
                
                /* Only populated on SDK3 avatars */
                if (parameters != null)
                {
                    var filtered = new List<AvatarParameter>();
                    foreach (var param in parameters)
                        if (!DefaultParameterNames.Contains(param.field_Private_String_0))
                            filtered.Add(param);

                    if (filtered.Count > 0)
                    {
                        avatarList.AddSimpleButton($"Parameters: {filtered.Count}", () =>
                        {
                            var parameterList = ExpansionKitApi.CreateCustomQuickMenuPage(LayoutDescription.WideSlimList);
                            foreach (var parameter in filtered)
                            {
                                var name = parameter.field_Private_String_0;
                                var type = parameter.field_Private_EnumNPublicSealedvaUnBoInFl5vUnique_0;
                                switch (type)
                                {
                                    case VRC.Playables.AvatarParameter.EnumNPublicSealedvaUnBoInFl5vUnique.Bool:
                                        UiExpansion.AddToggleListItem(parameterList, name, (state) => { parameter.prop_Boolean_0 = state; }, () => { return parameter.prop_Boolean_0; });
                                        break;

                                    case VRC.Playables.AvatarParameter.EnumNPublicSealedvaUnBoInFl5vUnique.Int:
                                        UiExpansion.AddIntListItem(parameterList, name, (value) => { parameter.prop_Int32_1 = value; }, parameter.prop_Int32_1);
                                        break;

                                    case VRC.Playables.AvatarParameter.EnumNPublicSealedvaUnBoInFl5vUnique.Float:
                                        UiExpansion.AddFloatListItem(parameterList, name, (value) => { parameter.prop_Single_0 = value; }, parameter.prop_Single_0);
                                        break;

                                    default:
                                        MelonLogger.Msg(System.ConsoleColor.Red, $"Unsupported [{type}]: {name}");
                                        break;
                                }
                            }
                            parameterList.AddSimpleButton("Back", () => { parameterList.Hide(); AvatarList(player_name, close_on_exit); });
                            parameterList.Show();
                        });
                    }
                }
            }
            avatarList.AddSimpleButton("Back", () => { avatarList.Hide(); if (!close_on_exit) PlayerList(); });
            avatarList.Show();
        }

        private void BlendShapeList(SkinnedMeshRenderer renderer, string player_name, bool close_on_exit)
        {
            var list = ExpansionKitApi.CreateCustomQuickMenuPage(LayoutDescription.WideSlimList);

            var mesh = renderer.sharedMesh;
            for (int i = 0; i < mesh.blendShapeCount; ++i)
            {
                var tmp = i;
                MelonLogger.Msg($"shapekey: {tmp} {mesh.GetBlendShapeName(tmp)} {renderer.GetBlendShapeWeight(tmp)}");
                UiExpansion.AddFloatListItem(list, mesh.GetBlendShapeName(i), (value) => { MelonLogger.Msg($"shapekey: {tmp} {value}"); renderer.SetBlendShapeWeight(tmp, value); }, renderer.GetBlendShapeWeight(tmp), 0.0f, 100.0f);
            }
            list.AddSimpleButton("Back", () => { list.Hide(); AvatarList(player_name, close_on_exit); });
            list.Show();
        }

        private delegate void AvatarInstantiatedDelegate(IntPtr @this, IntPtr avatarPtr, IntPtr avatarDescriptorPtr, bool loaded);
        private static AvatarInstantiatedDelegate _onAvatarInstantiatedDelegate;

        private static void OnAvatarInstantiated(IntPtr @this, IntPtr avatarPtr, IntPtr avatarDescriptorPtr, bool loaded)
        {
            /* Invoke original function pointer. */
            _onAvatarInstantiatedDelegate(@this, avatarPtr, avatarDescriptorPtr, loaded);

            try
            {
                if (loaded)
                {
                    GameObject avatar = new GameObject(avatarPtr);
                    string player_name = avatar.transform.root.GetComponentInChildren<VRCPlayer>().prop_String_0;
                    s_PlayerList[player_name] = avatar;
                    MelonLogger.Msg(ConsoleColor.Green, $"Checking {player_name} for coomer shader");
                    WitchHunt(avatar);
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error("An exception was thrown while working!\n" + ex.ToString());
            }
        }

    }
}
