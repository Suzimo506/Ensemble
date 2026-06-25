using UnityEngine;

namespace MDEN.UI.Core
{
    /// <summary>
    /// Simple data class replacing PopupLib.UI.Components.ForumObject.
    /// Holds the title, content, thumbnail texture, and metadata for a single list item
    /// displayed in <see cref="NativeListWindow"/>.
    /// </summary>
    public sealed class NativeListItem
    {
        /// <summary>Replaces ForumObject.Titles (was LocalString).</summary>
        public string Title { get; set; }

        /// <summary>Replaces ForumObject.Contents (was LocalString).</summary>
        public string Content { get; set; }

        /// <summary>Replaces ForumObject.Texture.</summary>
        public Texture2D Texture { get; set; }

        /// <summary>Replaces ForumObject.TextureURL.</summary>
        public string TextureURL { get; set; }

        /// <summary>Replaces ForumObject.IsNew.</summary>
        public bool IsNew { get; set; }

        public NativeListItem(string title, string content)
        {
            Title = title ?? string.Empty;
            Content = content ?? string.Empty;
        }
    }
}
