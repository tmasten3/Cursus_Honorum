using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.CharacterDetail
{
    public static class CharacterDetailPanelBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            CreatePanelIfNeeded();
        }

        private static void CreatePanelIfNeeded()
        {
            if (Object.FindFirstObjectByType<CharacterDetailPanel>() != null)
                return;

            var go = new GameObject("CharacterDetailCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CharacterDetailPanel));
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            Object.DontDestroyOnLoad(go);
        }
    }
}
