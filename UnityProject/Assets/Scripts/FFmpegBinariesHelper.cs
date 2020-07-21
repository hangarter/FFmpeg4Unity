using System;
using System.IO;
using FFmpeg.AutoGen;
using UnityEngine;


public class FFmpegBinariesHelper
{
    internal static void RegisterFFmpegBinaries()
    {
        ffmpeg.RootPath = "/usr/local/Cellar/ffmpeg/4.2.3_1/lib";
        return;
    }
}