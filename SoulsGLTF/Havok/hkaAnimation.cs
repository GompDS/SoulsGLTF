using System.Xml;
using DarkSrc.Util.Havok;
using SoulsAssetPipeline.XmlStructs;

namespace SoulsGLTF.Havok;

public class hkaAnimation : hkReferencedObject
{
    public double Duration { get; set; }
    public int NumberOfTransformTracks { get; set; }
    public int NumberOfFloatTracks { get; set; }
    public hkaAnimatedReferenceFrame? ExtractedMotion { get; set; }
    public hkaAnnotationTrack[] AnnotationTracks { get; set; }
    
    public virtual XmlNode? ReadXml(XmlNode node)
    {
        XmlNode exitNode = node;
        
        foreach (XmlNode childNode in node.ChildNodes)
        {
            string paramName = childNode.SafeGetAttribute("name");
            switch (paramName)
            {
                case "duration":
                    Duration = double.Parse(childNode.InnerText);
                    break;
                case "numberOfTransformTrack":
                    NumberOfTransformTracks = int.Parse(childNode.InnerText);
                    break;
                case "numberOfFloatTracks":
                    NumberOfFloatTracks = int.Parse(childNode.InnerText);
                    break;
                case "extractedMotion":
                    exitNode = exitNode.NextSibling;
                    if (childNode.InnerText != "null")
                    {
                        hkaAnimatedReferenceFrame? referenceFrame = null;
                        if (exitNode.SafeGetAttribute("class") == "hkaDefaultAnimatedReferenceFrame")
                        {
                            referenceFrame = new hkaAnimatedReferenceFrame();
                            referenceFrame.Name = exitNode.SafeGetAttribute("name");
                            exitNode = referenceFrame.ReadXml(exitNode);
                        }

                        if (referenceFrame != null)
                        {
                            ExtractedMotion = referenceFrame;
                        }
                    }
                    break;
                case "annotationTracks":
                    int annotationTrackCount = int.Parse(childNode.SafeGetAttribute("numelements"));
                    AnnotationTracks = new hkaAnnotationTrack[annotationTrackCount];
                    for (int i = 0; i < annotationTrackCount; i++)
                    {
                        XmlNode annotationTrackNode = childNode.ChildNodes[i];
                        hkaAnnotationTrack annotationTrack = new hkaAnnotationTrack();
                        annotationTrack.TrackName = annotationTrackNode.FirstChild.InnerText;
                        AnnotationTracks[i] = annotationTrack;
                    }
                    break;
            }
        }

        return exitNode;
    }
}