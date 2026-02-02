using System.Collections.Generic;
using UnityEngine;

public class BeatmapParser : MonoBehaviour
{
    private float sliderMultiplier = 1.4f; // Default if missing
    
    private struct TimingPoint
    {
        public float time;
        public float beatLength; // ms per beat. 60000/BPM.
        public bool isInherited; // If true, it changes SV (slider velocity) only? No, uninherited changes BPM.
        // Osu: 1 = Uninherited (Red Line, changes BPM), 0 = Inherited (Green Line, changes SV).
        // Actually flags are usually 6th or 7th param.
        // Format: time,beatLength,meter,sampleSet,sampleIndex,volume,uninherited,effects
        // uninherited: 1=BPM change, 0=Velocity change
    }
    
    private List<TimingPoint> timingPoints = new List<TimingPoint>();

    public List<MapData.HitInfo> Parse(string osuContent)
    {
        List<MapData.HitInfo> hitList = new List<MapData.HitInfo>();
        timingPoints.Clear();
        sliderMultiplier = 1.4f;

        string[] lines = osuContent.Split('\n');
        string currentSection = "";

        foreach (string line in lines)
        {
            string trim = line.Trim();
            if (string.IsNullOrEmpty(trim)) continue;

            if (trim.StartsWith("["))
            {
                currentSection = trim;
                continue;
            }

            if (currentSection == "[Difficulty]")
            {
                if (trim.StartsWith("SliderMultiplier:"))
                {
                    string val = trim.Substring("SliderMultiplier:".Length).Trim();
                    float.TryParse(val, out sliderMultiplier);
                }
            }
            else if (currentSection == "[TimingPoints]")
            {
                // Format: time,beatLength,meter,sampleSet,sampleIndex,volume,uninherited,effects
                // 331,600,4,1,0,10,1,0
                string[] parts = trim.Split(',');
                if (parts.Length >= 2)
                {
                    TimingPoint tp = new TimingPoint();
                    if (float.TryParse(parts[0], out float t) && float.TryParse(parts[1], out float bl))
                    {
                        tp.time = t;
                        tp.beatLength = bl;
                        // Determine inherited
                        tp.isInherited = false; // Default
                        if (parts.Length >= 7)
                        {
                            // 0 = Inherited (Green), 1 = Uninherited (Red)
                            // We need to differentiate for calculation.
                            // If Uninherited (1), beatLength is ms/beat.
                            // If Inherited (0), beatLength is negative inverse multiplier usually.
                            // Let's trust 0/1 flag first.
                            if (int.TryParse(parts[6], out int uninherited))
                            {
                                tp.isInherited = (uninherited == 0);
                            }
                        }
                        timingPoints.Add(tp);
                    }
                }
            }
            else if (currentSection == "[HitObjects]")
            {
                // Format: x,y,time,type,hitSound,params...
                // params for slider: curve|p1|p2..., slides, length, edgeSounds, edgeSets, hitSample
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
                hit.lane = Mathf.Clamp(x / 128, 0, 3);

                // Y -> Floor
                if (y < 128) hit.floor = 2;      // Top 3F
                else if (y < 256) hit.floor = 1; // Mid 2F
                else hit.floor = 0;              // Bot 1F

                // Type Checking (Bitwise)
                // 1 (Circle), 2 (Slider), 5 (Circle+NewCombo), 6 (Slider+NewCombo)
                // 128 (Mania Hold)
                bool isSlider = (typeMask & 2) != 0; 
                
                if (isSlider)
                {
                    hit.type = Note.NoteType.Long;
                    hit.curvePoints = new System.Collections.Generic.List<Vector2Int>();
                    
                    // Parse Curve Points
                    // Format: x,y,time,type,hitSound,curvePoints,slides,pixelLength,...
                    // Index:  0 1 2    3    4        5           6      7
                    if (parts.Length >= 6)
                    {
                        string[] curveParts = parts[5].Split('|');
                        // First part implies type (e.g. B), others are x:y
                        // Start from 1? Actually formatting is "Type|x:y|x:y..."
                        
                        // Add Start Point First (Lane/Floor already parsed)
                        hit.curvePoints.Add(new Vector2Int(hit.lane, hit.floor));

                        for (int i = 1; i < curveParts.Length; i++)
                        {
                            string[] xy = curveParts[i].Split(':');
                            if (xy.Length == 2)
                            {
                                if (int.TryParse(xy[0], out int cx) && int.TryParse(xy[1], out int cy))
                                {
                                    // Map to Game Coords
                                    int cLane = Mathf.Clamp(cx / 128, 0, 3);
                                    int cFloor = 0;
                                    if (cy < 128) cFloor = 2;
                                    else if (cy < 256) cFloor = 1;
                                    else cFloor = 0;
                                    
                                    // Add only if different from last to avoid dupes/noise
                                    Vector2Int last = hit.curvePoints[hit.curvePoints.Count - 1];
                                    if (last.x != cLane || last.y != cFloor)
                                    {
                                        hit.curvePoints.Add(new Vector2Int(cLane, cFloor));
                                    }
                                }
                            }
                        }
                    }

                    // Format: ...slides,pixelLength
                    if (parts.Length >= 8)
                    {
                        // Parse Repeats (Slides)
                        int repeats = 1;
                        if (int.TryParse(parts[6], out int r)) repeats = r;

                        if (float.TryParse(parts[7], out float pixelLength))
                        {
                            float duration = CalculateSliderDuration(timeMs, pixelLength, repeats);
                            hit.length = duration; // Seconds
                        }
                        else
                        {
                             hit.length = 1.0f; // Fallback
                        }
                    }
                    else
                    {
                        hit.length = 1.0f; // Fallback
                    }
                }
                else if ((typeMask & 128) != 0) // Mania Hold
                {
                    hit.type = Note.NoteType.Long;
                    // Format: x,y,time,type,hitSound,endTime:hitSample
                    // endTime is inside extra part at index 5
                    if (parts.Length >= 6)
                    {
                        string[] extras = parts[5].Split(':');
                        if (extras.Length > 0 && float.TryParse(extras[0], out float endTime))
                        {
                            hit.length = (endTime - timeMs) / 1000f;
                        }
                    }
                }
                else
                {
                    hit.type = Note.NoteType.Normal;
                    hit.length = 1.0f;
                }

                // If calculated length is tiny (e.g. malformed), clamp it
                if (hit.type == Note.NoteType.Long && hit.length < 0.1f) hit.length = 0.1f;

                hitList.Add(hit);
            }
        }

        return hitList;
    }

    private float CalculateSliderDuration(float timeMs, float pixelLength, int repeats)
    {
        // Formula: Duration = pixelLength / (100 * Multiplier * SV) * BeatDuration
        
        // 1. Find active TimingPoint (Red Line - Uninherited) for BPM
        TimingPoint redPoint = new TimingPoint { beatLength = 600f }; // Default 100bpm
        
        // Find latest red point <= timeMs
        foreach (var tp in timingPoints)
        {
            if (!tp.isInherited && tp.time <= timeMs)
            {
                redPoint = tp;
            }
        }
        
        // 2. Find active Velocity Multiplier (Green Line - Inherited)
        float svMultiplier = 1.0f;
        
        // Find latest point (Red or Green) <= timeMs to check for SV
        // Note: Red points usually reset SV to 1.0 unless a Green follows immediately? 
        // Logic: Iterate all, if isInherited update SV, if uninherited reset SV?
        // Actually, just find the LATEST point regardless.
        TimingPoint currentPoint = redPoint;
        foreach (var tp in timingPoints)
        {
            if (tp.time <= timeMs) currentPoint = tp;
        }
        
        if (currentPoint.isInherited)
        {
            // inherited beatLength is negative inverse percentage
            // -100 = 1.0x, -50 = 2.0x, -200 = 0.5x
            if (currentPoint.beatLength < 0)
            {
                svMultiplier = 100f / -currentPoint.beatLength;
            }
            else
            {
                svMultiplier = 1.0f; // Positive value in inherited usually doesn't happen for SV in strict osu logic?
            }
        }
        
        // BeatDuration = redPoint.beatLength (ms)
        // Velocity = 100 * SliderMultiplier * svMultiplier (pixels per beat)
        
        float velocity = 100.0f * sliderMultiplier * svMultiplier;
        
        // Beats needed = pixelLength / velocity
        // Total Beats = Single Beats * Repeats
        float beats = (pixelLength * repeats) / velocity;
        
        // Duration = beats * beatDuration (ms)
        // Return seconds
        float durationMs = beats * redPoint.beatLength;
        return durationMs / 1000.0f;
    }
}
