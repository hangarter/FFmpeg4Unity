using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using Assert = UnityEngine.Assertions.Assert;

namespace Tests
{
    public class VideoReaderTest
    {
        [UnityTest]
        public IEnumerator DecodeFrameToTexture2D_Frame5_ShouldSaveImage()
        {
            // Given
            // var sourceFile = "/Users/madison/Dropbox/game_development/VideoSnapshot/Assets/Tests/capture123.h264";
            var videoDecoder = new VideoDecoder();
            var sourceFile = "rtsp://wowzaec2demo.streamlock.net/vod/mp4:BigBuckBunny_115k.mov";
            var frameIndex = 50;
            
            // When
            videoDecoder.Run(sourceFile, frameIndex);
            
            // Then
            yield return null;
            Assert.IsTrue(UnityEngine.Windows.File.Exists($"image_{frameIndex:D5}.png"));
        }
    }
    
}