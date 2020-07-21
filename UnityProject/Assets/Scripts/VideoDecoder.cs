using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Drawing;
using System.IO;
using FFmpeg.AutoGen;

public class VideoDecoder
{
    private bool _isRunning;

    public delegate void FrameEvent(byte[] frameImageBlob);

    public event FrameEvent OnFrameRendered;

    public void Run(String filename, int frameIndex)
    {
        _isRunning = true;

        Debug.Log("Current directory: " + Environment.CurrentDirectory);
        var mode = Environment.Is64BitProcess ? "64" : "32";
        Debug.Log($"Running in {mode}-bit mode");

        FFmpegBinariesHelper.RegisterFFmpegBinaries();

        Debug.Log($"FFmpeg version info: {ffmpeg.av_version_info()}");

        SetupLogging();

        ConfigureHWDecoder(out var deviceType);

        Debug.Log("Decoding...");

        var texture2D = DecodeFrameToTexture2D(filename, frameIndex, deviceType);

        // _isRunning = false;
        // File.WriteAllBytes($"image_{frameIndex:D5}.png", texture2D.EncodeToPNG());
    }

    private void ConfigureHWDecoder(out AVHWDeviceType HWtype)
    {
        HWtype = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
        Debug.Log("Use hardware acceleration for decoding?[n]");
        var key = Console.ReadLine();
        var availableHWDecoders = new Dictionary<int, AVHWDeviceType>();
        if (key == "y")
        {
            Debug.Log("Select hardware decoder:");
            var type = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
            var number = 0;
            while ((type = ffmpeg.av_hwdevice_iterate_types(type)) != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
            {
                Debug.Log($"{++number}. {type}");
                availableHWDecoders.Add(number, type);
            }

            if (availableHWDecoders.Count == 0)
            {
                Debug.Log("Your system have no hardware decoders.");
                HWtype = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
                return;
            }

            int decoderNumber = availableHWDecoders
                .SingleOrDefault(t => t.Value == AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2).Key;
            if (decoderNumber == 0)
                decoderNumber = availableHWDecoders.First().Key;
            Debug.Log($"Selected [{decoderNumber}]");
            int.TryParse(Console.ReadLine(), out var inputDecoderNumber);
            availableHWDecoders.TryGetValue(inputDecoderNumber == 0 ? decoderNumber : inputDecoderNumber,
                out HWtype);
        }
    }

    private unsafe void SetupLogging()
    {
        ffmpeg.av_log_set_level(ffmpeg.AV_LOG_VERBOSE);

        // do not convert to local function
        av_log_set_callback_callback logCallback = (p0, level, format, vl) =>
        {
            if (level > ffmpeg.av_log_get_level()) return;

            var lineSize = 1024;
            var lineBuffer = stackalloc byte[lineSize];
            var printPrefix = 1;
            ffmpeg.av_log_format_line(p0, level, format, vl, lineBuffer, lineSize, &printPrefix);
            var line = Marshal.PtrToStringAnsi((IntPtr) lineBuffer);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(line);
            Console.ResetColor();
        };

        ffmpeg.av_log_set_callback(logCallback);
    }

    private Texture2D DecodeFrameToTexture2D(String filename, int frameIndex = 10,
        AVHWDeviceType HWDevice = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
    {
        using (var vsd = new VideoStreamDecoder(filename, HWDevice))
        {
            Debug.Log($"codec name: {vsd.CodecName}");

            var info = vsd.GetContextInfo();
            info.ToList().ForEach(x => Debug.Log($"{x.Key} = {x.Value}"));

            var sourceSize = vsd.FrameSize;
            var sourcePixelFormat = HWDevice == AVHWDeviceType.AV_HWDEVICE_TYPE_NONE
                ? vsd.PixelFormat
                : GetHWPixelFormat(HWDevice);
            var destinationSize = sourceSize;
            var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;
            using (var vfc = new VideoFrameConverter(sourceSize, sourcePixelFormat, destinationSize,
                destinationPixelFormat))
            {
                var currentFrame = 0;

                while (vsd.TryDecodeNextFrame(out var frame) && _isRunning)
                {
                    Debug.Log($"Processing frame: {currentFrame}");
                    var avframe = vfc.Convert(frame);
                    if (OnFrameRendered != null)
                    {
                        byte[] imageData;
                        vsd.AvFrameToImageByteArray(avframe, out imageData);
                        OnFrameRendered(imageData);
                    }

                    if (currentFrame == frameIndex)
                    {
                        Debug.Log($"Saving frame: {frameIndex}");
                        return vsd.AVFrameToTexture2D(avframe);
                    }

                    currentFrame++;
                }

                return new Texture2D(4, 4);
            }
        }
    }


    private AVPixelFormat GetHWPixelFormat(AVHWDeviceType hWDevice)
    {
        switch (hWDevice)
        {
            case AVHWDeviceType.AV_HWDEVICE_TYPE_NONE:
                return AVPixelFormat.AV_PIX_FMT_NONE;
            case AVHWDeviceType.AV_HWDEVICE_TYPE_VDPAU:
                return AVPixelFormat.AV_PIX_FMT_VDPAU;
            case AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA:
                return AVPixelFormat.AV_PIX_FMT_CUDA;
            case AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI:
                return AVPixelFormat.AV_PIX_FMT_VAAPI;
            case AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2:
                return AVPixelFormat.AV_PIX_FMT_NV12;
            case AVHWDeviceType.AV_HWDEVICE_TYPE_QSV:
                return AVPixelFormat.AV_PIX_FMT_QSV;
            case AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX:
                return AVPixelFormat.AV_PIX_FMT_VIDEOTOOLBOX;
            case AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA:
                return AVPixelFormat.AV_PIX_FMT_NV12;
            case AVHWDeviceType.AV_HWDEVICE_TYPE_DRM:
                return AVPixelFormat.AV_PIX_FMT_DRM_PRIME;
            case AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL:
                return AVPixelFormat.AV_PIX_FMT_OPENCL;
            case AVHWDeviceType.AV_HWDEVICE_TYPE_MEDIACODEC:
                return AVPixelFormat.AV_PIX_FMT_MEDIACODEC;
            default:
                return AVPixelFormat.AV_PIX_FMT_NONE;
        }
    }

    public void Stop()
    {
        _isRunning = false;
    }
    
}