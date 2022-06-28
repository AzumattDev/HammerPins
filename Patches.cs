using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace HammerPins;

public class DisplayPinsOnMap
{
    private static readonly Dictionary<ZDO, Minimap.PinData> customPins = new();
    private static readonly Dictionary<int, Sprite> icons = new();
    private static readonly int hammerHashCode = "Hammer".GetStableHashCode();
    private static readonly float updateInterval = 5.0f;
    internal static string[] PrefabArray = null!;

    // clear dictionary if the user logs out
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.OnDestroy))]
    public static class MinimapOnDestroyPatch
    {
        private static void Postfix()
        {
            customPins.Clear();
            icons.Clear();
            Array.Clear(PrefabArray, 0, PrefabArray.Length);
        }
    }

    [HarmonyPatch(typeof(Minimap), nameof(Minimap.UpdateMap))]
    public static class MinimapUpdateMapPatch
    {
        private static float timeCounter = updateInterval;

        public static void Postfix(ref Minimap __instance, Player player, float dt, bool takeInput)
        {
            timeCounter += dt;

            /*if (timeCounter < updateInterval || !OdinQOLplugin.mapIsEnabled.Value ||
                !HammerPinsPlugin.displayPins.Value)
                return;*/
            if (timeCounter < updateInterval ||
                HammerPinsPlugin.DisplayPins.Value == HammerPinsPlugin.Toggle.Off)
                return;
            if (HammerPinsPlugin.IconsToPin != null && HammerPinsPlugin.IconsToPin.Value.Length > 0)
            {
                PrefabArray = HammerPinsPlugin.IconsToPin.Value.Trim().Split(',').ToArray();
            }
            else
            {
                customPins.Clear();
                icons.Clear();
                Array.Clear(PrefabArray, 0, PrefabArray.Length);
                return;
            }

            timeCounter -= updateInterval;

            if (icons.Count == 0)
                FindIcons();


            // search zones for ships and carts
            foreach (List<ZDO> zdoarray in ZDOMan.instance.m_objectsBySector)
                if (zdoarray != null)
                    foreach (ZDO zdo in zdoarray)
                    {
                        foreach (string prefab in PrefabArray)
                        {
                            if (CheckPin(__instance, player, zdo, prefab.GetStableHashCode(), ""))
                                continue;
                        }
                    }

            // clear pins for destroyed objects
            foreach (KeyValuePair<ZDO, Minimap.PinData> pin in customPins.Where(pin => !pin.Key.IsValid()))
            {
                __instance.RemovePin(pin.Value);
                customPins.Remove(pin.Key);
            }
        }

        private static bool CheckPin(Minimap __instance, Player player, ZDO zdo, int hashCode, string pinName)
        {
            if (zdo.m_prefab != hashCode)
                return false;

            Minimap.PinData customPin;
            bool pinWasFound = customPins.TryGetValue(zdo, out customPin);

            // turn off associated pin if player controlled ship is in that position
            Ship controlledShip = player.GetControlledShip();
            if (controlledShip && Vector3.Distance(controlledShip.transform.position, zdo.m_position) < 0.01f)
            {
                if (pinWasFound)
                {
                    __instance.RemovePin(customPin);
                    customPins.Remove(zdo);
                }

                return true;
            }

            if (!pinWasFound)
            {
                customPin = __instance.AddPin(zdo.m_position, Minimap.PinType.Death, pinName, false, false);

                Sprite sprite;
                if (icons.TryGetValue(hashCode, out sprite))
                    customPin.m_icon = sprite;

                customPin.m_doubleSize = false;
                customPins.Add(zdo, customPin);
            }
            else
            {
                customPin.m_pos = zdo.m_position;
            }

            return true;
        }

        private static void FindIcons()
        {
            GameObject hammer = ObjectDB.instance.m_itemByHash[hammerHashCode];
            if (!hammer)
                return;
            ItemDrop hammerDrop = hammer.GetComponent<ItemDrop>();
            if (!hammerDrop)
                return;
            PieceTable hammerPieceTable = hammerDrop.m_itemData.m_shared.m_buildPieces;
            foreach (Piece p in hammerPieceTable.m_pieces.Select(piece => piece.GetComponent<Piece>()))
            {
                icons.Add(p.name.GetStableHashCode(), p.m_icon);
            }
        }
    }
}