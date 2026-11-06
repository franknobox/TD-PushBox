using System;
using System.Collections.Generic;
using UnityEngine;

namespace TD.PushBox
{
    [Serializable]
    public sealed class PushBoxLevelEntry
    {
        public string guid,scenePath;
        public Texture2D preview;
        [HideInInspector] public string previewHash;
    }

    public sealed class PushBoxLevelCatalog : ScriptableObject
    {
        public const string ResourceName="PushBoxLevels";
        public List<PushBoxLevelEntry> levels=new();
        public PushBoxLevelEntry Find(string guid)=>string.IsNullOrEmpty(guid)?null:levels.Find(level=>level.guid==guid);
    }
}
