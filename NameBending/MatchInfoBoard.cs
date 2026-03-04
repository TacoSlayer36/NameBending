using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players.Subsystems;
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

namespace NameBending
{
    partial class Core
    {
        bool hasMatchInfo = false;
        bool matchInfoSetUp = false;

        private GameObject player1TagClone = null;
        private GameObject player2TagClone = null;

        private IEnumerator setUpMatchInfo(string scene)
        {
            if (!matchInfoSetUp)
            {
                yield return new WaitForSeconds(3f);

                // Duplicate name tags
                Il2CppRUMBLE.Players.Player player1 = null;
                Il2CppRUMBLE.Players.Player player2 = null;

                try
                {
                    player1 = PlayerManager.Instance.AllPlayers[1];
                }
                catch
                { }

                try
                {
                    player2 = PlayerManager.Instance.AllPlayers[0];
                }
                catch
                { }

                if (player1 == null || player2 == null || scene != sceneName)
                {
                    yield break;
                }

                GameObject player1Tag = player1.Controller.transform.Find("NameTag").gameObject;
                if (player1Tag != null)
                {
                    player1TagClone = GameObject.Instantiate(player1Tag);
                    PlayerNameTag tagComponent = player1Tag.GetComponent<Il2CppRUMBLE.Players.Subsystems.PlayerNameTag>();
                    tagComponent.parentController = player1.Controller;

                    player1TagClone.SetActive(false);
                    player1TagClone.transform.GetChild(0).GetComponent<TextMeshPro>().text = player1Tag.transform.GetChild(0).GetComponent<TextMeshPro>().text;
                    player1TagClone.transform.GetChild(3).GetComponent<TextMeshPro>().text = player1Tag.transform.GetChild(3).GetComponent<TextMeshPro>().text;
                }
                GameObject player2Tag = player2.Controller.transform.Find("NameTag").gameObject;
                if (player2Tag != null)
                {
                    player2TagClone = GameObject.Instantiate(player2Tag);
                    PlayerNameTag tagComponent = player2TagClone.GetComponent<Il2CppRUMBLE.Players.Subsystems.PlayerNameTag>();
                    tagComponent.parentController = player2.Controller;

                    tagComponent.UpdatePlayerBPText();
                    tagComponent.UpdatePlayerNameTagColor();
                    tagComponent.UpdatePlayerRankIcon();
                    tagComponent.UpdatePlayerTitleText();
                    tagComponent.UpdatePlayerNameText();

                    player2TagClone.SetActive(false);
                    player2TagClone.transform.GetChild(3).GetComponent<TextMeshPro>().text = player2Tag.transform.GetChild(3).GetComponent<TextMeshPro>().text;
                    player2TagClone.transform.GetChild(0).GetComponent<TextMeshPro>().text = player2Tag.transform.GetChild(0).GetComponent<TextMeshPro>().text;
                }

                yield return new WaitForSeconds(3f);

                if (sceneName == "Map0")
                {
                    if (player1TagClone != null)
                    {
                        player1TagClone.transform.position = new Vector3(-2.51f, 0.27f, 20f);
                    }
                    if (player2TagClone != null)
                    {
                        player2TagClone.transform.position = new Vector3(0.51f, 0.27f, 20f);
                    }
                }
                if (sceneName == "Map1")
                {
                    if (player1TagClone != null)
                    {
                        player1TagClone.transform.position = new Vector3(-1.51f, 6.02f, 10.5f);
                        MelonLogger.Msg(player1TagClone.transform.position);
                    }
                    if (player2TagClone != null)
                    {
                        player2TagClone.transform.position = new Vector3(1.51f, 6.02f, 10.5f);
                        MelonLogger.Msg(player2TagClone.transform.position);
                    }
                }

                if (player1TagClone != null)
                {
                    player1TagClone.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
                    player1TagClone.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    player1TagClone.SetActive(true);
                    PlayerNameTag tagComponent = player1TagClone.GetComponent<PlayerNameTag>();
                    tagComponent.parentController = player1.Controller;
                    tagComponent.FadePlayerNameTag(true);
                }

                if (player2TagClone != null)
                {
                    player2TagClone.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
                    player2TagClone.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    player2TagClone.SetActive(true);
                    PlayerNameTag tagComponent = player2TagClone.GetComponent<PlayerNameTag>();
                    tagComponent.parentController = player2.Controller;
                    tagComponent.FadePlayerNameTag(true);
                }

                yield return new WaitForSeconds(1f);

                GameObject sign = GameObject.Find("MatchInfoMod");

                // Move sign forward slightly (so the tags don't clip into the wall)
                sign.transform.position = new Vector3(sign.transform.position.x, sign.transform.position.y, sign.transform.position.z - 0.501f);
                sign.transform.Find("Player1Name").gameObject.active = false;
                sign.transform.Find("Player2Name").gameObject.active = false;
                sign.transform.Find("Player1BP").gameObject.active = false;
                sign.transform.Find("Player2BP").gameObject.active = false;
            }

            matchInfoSetUp = true;
        }
    }
}