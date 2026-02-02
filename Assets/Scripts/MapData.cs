public class MapData
{
    public class HitInfo
    {
        public float time;       // Hit time in seconds
        public int lane;         // 0, 1, 2, 3
        public int floor;        // 0 (1F), 1 (2F), 2 (3F)
        public Note.NoteType type;
        public float length;     // Duration/Length for Long Notes
        public System.Collections.Generic.List<UnityEngine.Vector2Int> curvePoints; // X=Lane, Y=Floor
    }
}
