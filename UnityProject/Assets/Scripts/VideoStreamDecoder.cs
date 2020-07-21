using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using FFmpeg.AutoGen;
using UnityEditor;

public sealed unsafe class VideoStreamDecoder : IDisposable
{
    private AVCodecContext* _pCodecContext;
    private AVFormatContext* _pFormatContext;
    private int _streamIndex;
    private AVFrame* _pFrame;
    private AVFrame* _receivedFrame;
    private AVPacket* _pPacket;
    private int _offset;
    private AVIOContext* _avioContext;

    public VideoStreamDecoder(string fileName, AVHWDeviceType HWDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
    {
        _pFormatContext = ffmpeg.avformat_alloc_context();

        if (_pFormatContext == null)
        {
            throw new Exception("Could not allocate the format context");
        }
        
        _receivedFrame = ffmpeg.av_frame_alloc();
        var pFormatContext = _pFormatContext;

        ffmpeg.avformat_open_input(&pFormatContext, fileName, null, null);
        ffmpeg.avformat_find_stream_info(_pFormatContext, null);

        AVCodec* codec = null;
        _streamIndex =
            ffmpeg.av_find_best_stream(_pFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &codec, 0);
        _pCodecContext = ffmpeg.avcodec_alloc_context3(codec);

        if (HWDeviceType != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
        {
            ffmpeg.av_hwdevice_ctx_create(&_pCodecContext->hw_device_ctx, HWDeviceType, null, null, 0);
        }

        ffmpeg.avcodec_parameters_to_context(_pCodecContext, _pFormatContext->streams[_streamIndex]->codecpar);
        ffmpeg.avcodec_open2(_pCodecContext, codec, null);

        CodecName = ffmpeg.avcodec_get_name(codec->id);
        FrameSize = new Size(_pCodecContext->width, _pCodecContext->height);
        PixelFormat = _pCodecContext->pix_fmt;

        _pPacket = ffmpeg.av_packet_alloc();
        _pFrame = ffmpeg.av_frame_alloc();
    }

    public string CodecName { get; set; }
    public Size FrameSize { get; set; }
    public AVPixelFormat PixelFormat { get; set; }

    public void Dispose()
    {
        ffmpeg.av_frame_unref(_pFrame);
        ffmpeg.av_free(_pFrame);

        ffmpeg.av_packet_unref(_pPacket);
        ffmpeg.av_free(_pPacket);

        ffmpeg.avcodec_close(_pCodecContext);
        var pFormatContext = _pFormatContext;
        ffmpeg.avformat_close_input(&pFormatContext);
        // File.Delete(fileName);
    }

    public bool TryDecodeNextFrame(out AVFrame frame)
    {
        ffmpeg.av_frame_unref(_pFrame);
        ffmpeg.av_frame_unref(_receivedFrame);
        int error;
        do
        {
            try
            {
                do
                {
                    error = ffmpeg.av_read_frame(_pFormatContext, _pPacket);
                    if (error == ffmpeg.AVERROR_EOF)
                    {
                        frame = *_pFrame;
                        return false;
                    }
                } while (_pPacket->stream_index != _streamIndex);

                ffmpeg.avcodec_send_packet(_pCodecContext, _pPacket);
            }
            finally
            {
                ffmpeg.av_packet_unref(_pPacket);
            }

            error = ffmpeg.avcodec_receive_frame(_pCodecContext, _pFrame);
        } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));

        if (_pCodecContext->hw_device_ctx != null)
        {
            ffmpeg.av_hwframe_transfer_data(_receivedFrame, _pFrame, 0);
            frame = *_receivedFrame;
        }
        else
        {
            frame = *_pFrame;
        }

        return true;
    }

    public IReadOnlyDictionary<string, string> GetContextInfo()
    {
        AVDictionaryEntry* tag = null;
        var result = new Dictionary<string, string>();
        while ((tag = ffmpeg.av_dict_get(_pFormatContext->metadata, "", tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
        {
            var key = Marshal.PtrToStringAnsi((IntPtr) tag->key);
            var value = Marshal.PtrToStringAnsi((IntPtr) tag->value);
            result.Add(key, value);
        }

        return result;
    }

    public void SavePng(AVFrame frame, string fileName)
    {
        var texture = AVFrameToTexture2D(frame);

        File.WriteAllBytes(fileName, texture.EncodeToPNG());

        Debug.Log($"Saved {fileName}");
    }

    public Texture2D AVFrameToTexture2D(AVFrame frame)
    {
        if (AvFrameToImageByteArray(frame, out var pngData)) return null;

        Texture2D texture = new Texture2D(5, 5);

        texture.LoadImage(pngData);
        return texture;
    }

    public bool AvFrameToImageByteArray(AVFrame frame, out byte[] pngData)
    {
        AVCodec* outCodec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_PNG);
        AVCodecContext* outCodecCtx = ffmpeg.avcodec_alloc_context3(outCodec);

        outCodecCtx->width = _pCodecContext->width;
        outCodecCtx->height = _pCodecContext->height;
        outCodecCtx->pix_fmt = AVPixelFormat.AV_PIX_FMT_RGB24;
        outCodecCtx->codec_type = AVMediaType.AVMEDIA_TYPE_VIDEO;
        outCodecCtx->time_base.num = _pCodecContext->time_base.num;
        outCodecCtx->time_base.den = _pCodecContext->time_base.den;

        if (ffmpeg.avcodec_open2(outCodecCtx, outCodec, null) < 0)
        {
            pngData = new byte[] { };
            return false;
        }

        AVPacket outPacket = new AVPacket();
        ffmpeg.av_init_packet(&outPacket);
        outPacket.size = 0;
        outPacket.data = null;

        ffmpeg.avcodec_send_frame(outCodecCtx, &frame);
        ffmpeg.avcodec_receive_packet(outCodecCtx, &outPacket);

        pngData = new byte[outPacket.size];

        Marshal.Copy((IntPtr) outPacket.data, pngData, 0, outPacket.size);
        return true;
    }
}