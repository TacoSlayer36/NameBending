using HarmonyLib;
using Il2CppRUMBLE.Environment.MatchFlow;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Subsystems;
using Il2CppRUMBLE.Slabs;
using MelonLoader;
using System;
using System.Collections;
using UnityEngine;

namespace NameBending;

public static class MatchInfoBoard
{
    public static GameObject Board;
    private static GameObject matchInfoPlayer1Name;
    private static GameObject matchInfoPlayer2Name;
    private static GameObject matchInfoPlayer1BP;
    private static GameObject matchInfoPlayer2BP;

    public static bool HasMatchInfo = false;
    public static GameObject PlateClone1;
    public static GameObject PlateClone2;

    public static IEnumerator FindMatchInfoBoard(Action callback = null)
    {
        int tries = 0;
        while (true)
        {
            if (tries++ > 30)
            {
                HasMatchInfo = false;
                callback?.Invoke();
                yield break;
            }

            Board = GameObject.Find("MatchInfoMod");
            if (Board != null) break;

            yield return new WaitForSeconds(0.5f);
        }

        matchInfoPlayer1Name = Board.transform.Find("Player1Name")?.gameObject;
        matchInfoPlayer2Name = Board.transform.Find("Player2Name")?.gameObject;
        matchInfoPlayer1BP = Board.transform.Find("Player1BP")?.gameObject;
        matchInfoPlayer2BP = Board.transform.Find("Player2BP")?.gameObject;

        HasMatchInfo = true;
        callback?.Invoke();
    }

    public static IEnumerator SetUpMatchInfo()
    {
        if (!Config.MatchInfoPlates.Value) yield break;
        if (PlayerManager.Instance.AllPlayers.Count < 2) yield break;
        if (!HasMatchInfo) yield break;

        PlayerController player1 = null;
        PlayerController player2 = null;

        int tries = 0;
        while (true)
        {
            yield return new WaitForFixedUpdate();

            if (tries++ > 500)
            {
                HasMatchInfo = false;
                yield break;
            }

            player1 = PlayerManager.Instance?.AllPlayers[0]?.Controller;
            player2 = PlayerManager.Instance?.AllPlayers[1]?.Controller;

            if (player1 == null || player2 == null) continue;

            PlateClone1 = Core.Instance.CloneNameplate(player1.transform.Find("NameTag").gameObject);
            PlateClone2 = Core.Instance.CloneNameplate(player2.transform.Find("NameTag").gameObject);

            if (PlateClone1 == null || PlateClone2 == null) continue;
           
            break;
        }

        SetUpMatchInfo();
        void SetUpMatchInfo()
        {
            PlateClone1.SetActive(true);
            PlateClone2.SetActive(true);

            PlateClone1.transform.SetParent(Board.transform);
            PlateClone1.transform.rotation = matchInfoPlayer2Name.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
            PlateClone1.transform.localPosition = new Vector3(1.37f, 0.78f, 0f);
            PlateClone1.transform.localScale = Vector3.one * 2.22f;

            PlateClone2.transform.SetParent(Board.transform);
            PlateClone2.transform.rotation = matchInfoPlayer2Name.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
            PlateClone2.transform.localPosition = new Vector3(-1.37f, 0.78f, 0f);
            PlateClone2.transform.localScale = Vector3.one * 2.22f;

            matchInfoPlayer1Name.SetActive(false);
            matchInfoPlayer2Name.SetActive(false);
            matchInfoPlayer1BP.SetActive(false);
            matchInfoPlayer2BP.SetActive(false);
        }
    }

    public static void ResetMatchInfo(bool nullifyBoard)
    {
        GameObject.Destroy(PlateClone1);
        GameObject.Destroy(PlateClone2);

        if (Board != null)
        {
            matchInfoPlayer1Name.SetActive(true);
            matchInfoPlayer2Name.SetActive(true);
            matchInfoPlayer1BP.SetActive(true);
            matchInfoPlayer2BP.SetActive(true);
        }
        if (nullifyBoard) Board = null;
    }

    public static void SetMatchInfo(bool enabled)
    {
        if (!enabled)
            ResetMatchInfo(true);
        else
        {
            if (Board == null)
                MelonCoroutines.Start(FindMatchInfoBoard(callback));

            void callback()
            {
                if (PlateClone1 == null || PlateClone2 == null)
                {
                    ResetMatchInfo(false);
                    MelonCoroutines.Start(SetUpMatchInfo());
                }
            }
        }
    }

    [HarmonyPatch(typeof(SlabOwnership), nameof(SlabOwnership.SetOwnership), new Type[] { typeof(Pedestal) })]
    public static class slabpatch
    {
        private static void Postfix()
        {
            if (!HasMatchInfo) return;

            if (PlateClone1 != null)
            {
                PlayerNameTag nameTag = PlateClone1.GetComponentInChildren<PlayerNameTag>();
                if (nameTag != null) nameTag.UpdatePlayerBPText();
            }

            if (PlateClone2 != null)
            {
                PlayerNameTag nameTag = PlateClone2.GetComponentInChildren<PlayerNameTag>();
                if (nameTag != null) nameTag.UpdatePlayerBPText();
            }
        }
    }
}