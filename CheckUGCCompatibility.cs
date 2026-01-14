using System;
using System.Collections.Generic;
using System.Linq;
using Landfall.Modding;
using Steamworks;
using Zorro.UI.Modal;
using UnityEngine;

namespace AethaModelSwapMod;

public static class CheckUGCCompatibility
{
    private const uint UpdateTime = 1767240000; // January 1 2026
    private static readonly PublishedFileId_t[] BadFileIds =
    {
    };
    
    private const string HeaderText = "Some mods may be incompatible with AethaModelSwap:\n";
    private const string OpenWorkshopText = "Open Workshop";
    private const string IgnoreText = "Ignore";

    public static async void CheckCompatibiltiy()
    {
        // Check if the bad mod is loaded: fastest path
        bool badFileId = false;
        foreach (var id in BadFileIds)
        {
            if (Modloader.LoadedItems.Contains(id))
            {
                badFileId = true;
                break;
            }
        }
        if (!badFileId)
        {
            return;
        }
        // Check if the bad mod has been updated: slower path
        foreach (var id in BadFileIds)
        {
            if (!ModManager.UgcItemsCache.ContainsKey(id))
            {
                try
                {
                    await ModManager.FetchUgcItems(BadFileIds, false, null);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                break;
            }
        }
        PopupIfIncompatible();
    }

    static void PopupIfIncompatible()
    {
        List<(string name, PublishedFileId_t id)> incompatibleMods = new();
        Debug.Log("Mods: "+string.Join(", ", ModManager.UgcItemsCache.Values.Select(x => x.Details.Title())));
        foreach (var id in BadFileIds)
        {
            if (ModManager.UgcItemsCache.TryGetValue((PublishedFileId_t)id, out var ugc))
            {
                if (ugc.Details.m_rtimeUpdated < UpdateTime)
                {
                    incompatibleMods.Add((ugc.Details.Title(), ugc.Details.m_nPublishedFileId));
                }
            }
        }
        if (!incompatibleMods.Any())
        {
            return;
        }
        var header = new DefaultHeaderModalOption(null, HeaderText+string.Join("\n", incompatibleMods.Select(x => x.name)));
        var options = new ModalButtonsOption(new[]
        {
            new ModalButtonsOption.Option(OpenWorkshopText, () => OpenToWorkshopPage(incompatibleMods[0].id)),
            new ModalButtonsOption.Option(IgnoreText, null),
        });
        Modal.OpenModal(header, options, null);
    }

    static void OpenToWorkshopPage(PublishedFileId_t id)
    {
        SteamFriends.ActivateGameOverlayToWebPage($"https://steamcommunity.com/sharedfiles/filedetails/?id={id.ToString()}");
    }
}