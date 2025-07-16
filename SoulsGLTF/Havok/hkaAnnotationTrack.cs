using System.Collections.Generic;

namespace DarkSrc.Util.Havok;

public class hkaAnnotationTrack
{
    public struct Annotation
    {
        public double Time;
        public string Text;
    }

    public string TrackName { get; set; } = "";
    public Annotation[] Annotations { get; set; }
}