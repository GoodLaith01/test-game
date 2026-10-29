using UnityEngine;

namespace Hollowmark
{
    /// <summary>
    /// Starts the game in any scene: open the project, press Play. No scene setup or prefabs are required,
    /// because the village and dungeons are generated from the JSON data at runtime.
    /// Add a GameObject named "NoAutoBoot" to a scene to opt that scene out.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (GameObject.Find("NoAutoBoot") != null) return;
            if (GameManager.Instance != null) return;
            var go = new GameObject("Hollowmark");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<GameManager>().Init();
        }
    }
}
