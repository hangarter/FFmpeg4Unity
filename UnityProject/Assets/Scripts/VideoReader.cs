using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class VideoReader 
{
    public static Texture TakeSnapshot(byte[] h264Stream, int frame = 0)
    {
        return new Texture3D(1, 1, 1, DefaultFormat.LDR, TextureCreationFlags.None);
    }


    public static void SaveFrame(int frame)
    {
        var data = File.ReadAllBytes("./Assets/Tests/capture123.h264.raw");

        // var mediaInfo = FFProbe.Analyse(inputFile);
    }
}
