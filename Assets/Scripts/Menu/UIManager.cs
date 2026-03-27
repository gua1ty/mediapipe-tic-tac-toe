using System;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject MainMenu;
    [SerializeField] private GameObject HostMenu;
    [SerializeField] private GameObject ClientMenu;
    [SerializeField] private TMPro.TMP_Text joinCodeText;
    [SerializeField] private Button joinButton;

    private void Start()
    {
        MainMenu.SetActive(true);
        HostMenu.SetActive(false);
        ClientMenu.SetActive(false);

        joinButton.onClick.AddListener(ShowClientMenu);
        ConnectionManager.Instance.OnJoinCodeGenerated += ShowHostMenu;
    }

    private void ShowHostMenu(string joinCode)
    {
        MainMenu.SetActive(false);
        HostMenu.SetActive(true);
        joinCodeText.text = joinCode;
    }

    private void ShowClientMenu()
    {
        MainMenu.SetActive(false);
        ClientMenu.SetActive(true);
    }
}