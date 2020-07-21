using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using FFmpeg.AutoGen;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class VideoPlayer : MonoBehaviour
{
    public Text fps;

    private VideoDecoder _videoDecoder;
    private SpriteRenderer _spriteRenderer;
    private Task _backgroundTask;
    private bool _frameUpdate;
    private byte[] _imageBlob;

    private double _deltaTime = 0;
    private double _fps = 0.0;

    private void Awake()
    {
        Assert.IsNotNull(fps);
    }

    // Start is called before the first frame update
    void Start()
    {
        _videoDecoder = new VideoDecoder();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _videoDecoder.OnFrameRendered += OnFrameRendered;
        
        _backgroundTask = Task.Run(() =>
        {
            var sourceFile = "rtsp://wowzaec2demo.streamlock.net/vod/mp4:BigBuckBunny_115k.mov";
            var frameIndex = 1000;
            _videoDecoder.Run(sourceFile, frameIndex);
        });
        // _backgroundTask.Wait();
    }

    private void Update()
    {
        if (_frameUpdate)
        {
            _spriteRenderer.sprite.texture.LoadImage(_imageBlob);
            // _spriteRenderer.sprite.texture.LoadRawTextureData(_imageBlob);
            // _spriteRenderer.sprite.texture.Apply();
            _frameUpdate = false;

            UpdateFPS();
        }
    }

    private void UpdateFPS()
    {
        _deltaTime += Time.deltaTime;
        _deltaTime /= 2.0;
        _fps = 1.0 / _deltaTime;
        fps.text = "FPS: " + _fps.ToString("F");
    }

    void OnFrameRendered(byte[] imageBlob)
    {
        _imageBlob = imageBlob;
        _frameUpdate = true;
    }

    private void OnDestroy()
    {
        _videoDecoder.Stop();
        // _backgroundTask.Wait();
    }
}