using System.Collections.Generic;
using UnityEngine;

public class BeatmapParser : MonoBehaviour
{
    public List<MapData.HitInfo> Parse(string osuContent)
    {
        List<MapData.HitInfo> hitList = new List<MapData.HitInfo>();
        
        string[] lines = osuContent.Split('\n');
        bool readHitObjects = false;

        foreach (string line in lines)
        {
            string trim = line.Trim();
            if (string.IsNullOrEmpty(trim)) continue;

            if (trim.StartsWith("[HitObjects]"))
            {
                readHitObjects = true;
                continue;
            }

            if (readHitObjects)
            {
                // Format: x,y,time,type,hitSound,params...
                string[] parts = trim.Split(',');
                if (parts.Length < 4) continue;

                int x = int.Parse(parts[0]);
                int y = int.Parse(parts[1]);
                float timeMs = float.Parse(parts[2]);
                int typeMask = int.Parse(parts[3]);

                // Map to Game Data
                MapData.HitInfo hit = new MapData.HitInfo();
                hit.time = timeMs / 1000f; // Convert ms to seconds
                
                // X (0-512) -> Lane (0-3)
                // 512 / 4 = 128
                hit.lane = Mathf.Clamp(x / 128, 0, 3);

                // Y (0-384) -> Floor (0, 1, 2)
                // osu! Y: Top is 0, Bottom is 384
                // Observed data:
                // High (~70) -> 3F (Index 2)
                // Mid (~200) -> 2F (Index 1)
                // Low (~320) -> 1F (Index 0)
                
                if (y < 128) hit.floor = 2;      // Top 3F
                else if (y < 256) hit.floor = 1; // Mid 2F
                else hit.floor = 0;              // Bot 1F

                // Type Bitmask
                // 1 (Circle) or 5 (Circle + NewCombo) -> Normal
                // 2 (Slider) or 6 (Slider + NewCombo) -> Long? (User said Slider = Long Note usually, let's map it)
                // 128 (Mania Hold) -> Long
                bool isSlider = (typeMask & 2) != 0;
                bool isHold = (typeMask & 128) != 0;

                if (isSlider || isHold)
                {
                    hit.type = Note.NoteType.Long;
                    // For Sliders, length calculation is complex (depends on pixel length & slider velocity).
                    // For now, let's assume a default length or try to parse if possible.
                    // But simplified osu parsing usually needs TimingPoints for Slider length.
                    // Given the constraint, we might just give a fixed duration or randomize slightly for visual effect?
                    // OR if it's Mania Hold (type 128), end time corresponds to `time:endtime` in extras.
                    // Let's stick to Normal for standard notes, and Long only if clearly Long.
                    // The snippet only showed Circles (type 1/5).
                    
                    // User Request: "Slider (bit 1) -> Long Note" logic is acceptable.
                    // Setting arbitrary length since calculating exact duration requires full beatmap logic
                    hit.length = 3.0f; 
                }
                else
                {
                    hit.type = Note.NoteType.Normal;
                    hit.length = 1.0f;
                }

                hitList.Add(hit);
            }
        }

        return hitList;
    }
}
