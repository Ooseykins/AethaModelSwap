using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace AethaModelSwapMod;

public static class AudioLoader
{
    private static Dictionary<string, AudioClip> allClips = new();
    public static IEnumerator<UnityWebRequestAsyncOperation> LoadNewClip(string path, SFX_Instance sfxTarget)
    {
        Debug.Log($"Load audio at path: {path}");
        if (allClips.ContainsKey(path))
        {
            AddClip(allClips[path], sfxTarget);
            yield break;
        }
        using UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(path, StringToAudioType(path));
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.ConnectionError)
        {
            Debug.LogError($"Error loading audio file: {www.error}");
            yield break;
        }
        var audioClip = DownloadHandlerAudioClip.GetContent(www);
        if (audioClip)
        {
            allClips[path] = audioClip;
            AddClip(audioClip, sfxTarget);
            Debug.Log($"Loaded clip: {path}");
        }
    }

    private static void AddClip(AudioClip clip, SFX_Instance sfxTarget)
    {
        var list = sfxTarget.clips.ToList();
        list.Add(clip);
        sfxTarget.clips = list.ToArray();
    }
    
    private static AudioType StringToAudioType(string path)
    {
        switch (path.Split(".").Last().ToLower())
        {
            case "wav":
                return AudioType.WAV;
            case "mp3":
                return AudioType.MPEG;
        }
        return AudioType.UNKNOWN;
    }
}