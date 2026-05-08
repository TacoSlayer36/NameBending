using HarmonyLib;
using Il2CppRUMBLE.Environment.MatchFlow;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Subsystems;
using Il2CppRUMBLE.Slabs;
using Il2CppTMPro;
using MelonLoader;
using RumbleModdingAPI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NameBending;

public static class MatchInfoBoard
{
    private static GameObject matchInfoBoard;
    private static GameObject matchInfoPlayer1Name;
    private static GameObject matchInfoPlayer2Name;
    private static GameObject matchInfoPlayer1BP;
    private static GameObject matchInfoPlayer2BP;

    private static GameObject player1TagClone = null;
    private static GameObject player2TagClone = null;

    public static bool HasMatchInfo = false;
    public static GameObject PlateClone1;
    public static GameObject PlateClone2;

    public static IEnumerator FindMatchInfoBoard()
    {
        yield return new WaitForSeconds(5f);

        try
        {
            GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>(true);
            matchInfoBoard = allObjects.FirstOrDefault(go => go.name == "MatchInfoMod");
            if (matchInfoBoard == null)
                yield break;

            matchInfoPlayer1Name = matchInfoBoard.transform.Find("Player1Name")?.gameObject;
            matchInfoPlayer2Name = matchInfoBoard.transform.Find("Player2Name")?.gameObject;
            matchInfoPlayer1BP = matchInfoBoard.transform.Find("Player1BP")?.gameObject;
            matchInfoPlayer2BP = matchInfoBoard.transform.Find("Player2BP")?.gameObject;

            HasMatchInfo = true;
        }
        catch
        {
            HasMatchInfo = false;
        }
    }

    public static IEnumerator SetUpMatchInfoDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetUpMatchInfo();
    }
    public static void SetUpMatchInfo()
    {
        if (PlayerManager.Instance.AllPlayers.Count < 2) return;
        if (!HasMatchInfo) return;

        PlayerController player1 = PlayerManager.Instance.AllPlayers[0]?.Controller;
        PlayerController player2 = PlayerManager.Instance.AllPlayers[1]?.Controller;

        PlateClone1 = Core.Instance.CloneNameplate(player1.transform.Find("NameTag").gameObject);
        PlateClone2 = Core.Instance.CloneNameplate(player2.transform.Find("NameTag").gameObject);

        PlateClone1.SetActive(true);
        PlateClone2.SetActive(true);

        PlateClone1.transform.SetParent(matchInfoBoard.transform);
        PlateClone1.transform.rotation = matchInfoPlayer2Name.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
        PlateClone1.transform.localPosition = new Vector3(1.37f, 0.78f, 0f);
        PlateClone1.transform.localScale = Vector3.one * 2.22f;

        PlateClone2.transform.SetParent(matchInfoBoard.transform);
        PlateClone2.transform.rotation = matchInfoPlayer2Name.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
        PlateClone2.transform.localPosition = new Vector3(-1.37f, 0.78f, 0f);
        PlateClone2.transform.localScale = Vector3.one * 2.22f;

        matchInfoPlayer1Name.SetActive(false);
        matchInfoPlayer2Name.SetActive(false);
        matchInfoPlayer1BP.SetActive(false);
        matchInfoPlayer2BP.SetActive(false);
    }

    public static void ResetMatchInfo()
    {
        GameObject.Destroy(PlateClone1);
        GameObject.Destroy(PlateClone2);

        if (matchInfoBoard == null) FindMatchInfoBoard();
        if (matchInfoBoard != null)
        {
            matchInfoPlayer1Name.SetActive(true);
            matchInfoPlayer2Name.SetActive(true);
            matchInfoPlayer1BP.SetActive(true);
            matchInfoPlayer2BP.SetActive(true);
        }
    }

    public static void SetMatchInfo(bool enabled)
    {
        if (!enabled)
            ResetMatchInfo();
        else
        {
            if (matchInfoBoard == null)
                FindMatchInfoBoard();

            if (PlateClone1 == null || PlateClone2 == null)
            {
                ResetMatchInfo();
                SetUpMatchInfo();
            }
        }
    }

    [HarmonyPatch(typeof(SlabOwnership), nameof(SlabOwnership.SetOwnership), new Type[] { typeof(Pedestal) })]
    public static class slabpatch
    {
        private static void Postfix()
        {
            PlateClone1?.GetComponentInChildren<PlayerNameTag>()?.UpdatePlayerBPText();
            PlateClone2?.GetComponentInChildren<PlayerNameTag>()?.UpdatePlayerBPText();
        }
    }
}