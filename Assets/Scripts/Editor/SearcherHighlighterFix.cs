using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace UnityEditor.Searcher
{
    public static class SearcherHighlighter
    {
        public static void HighlightTextBasedOnQuery(VisualElement container, string text, string query)
        {
            // Placeholder fix for missing SearcherHighlighter.cs in PackageCache
            container.Add(new Label(text));
        }
    }
}