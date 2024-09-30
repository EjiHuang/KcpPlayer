﻿// https://github.com/sipsorcery-org/SIPSorceryMedia.FFmpeg/blob/master/src/FFmpegCameraManager.cs

using DirectShowLib;
using FFmpeg.AutoGen;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;

namespace KcpPlayer.Avalonia.Utils;

public unsafe class FFmpegCameraManager
{
    static public List<Camera>? GetCameraDevices()
    {
        List<Camera>? result = null;

        string inputFormat = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dshow"
                                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "v4l2"
                                //: RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "avfoundation"
                                : throw new NotSupportedException($"Cannot find adequate input format - OSArchitecture:[{RuntimeInformation.OSArchitecture}] - OSDescription:[{RuntimeInformation.OSDescription}]");


        // FFmpeg doesn't implement avdevice_list_input_sources() for the DShow input format yet.
        if (inputFormat == "dshow")
        {
            result = new List<Camera>();
            var dsDevices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            for (int i = 0; i < dsDevices.Length; i++)
            {
                var dsDevice = dsDevices[i];
                if ((dsDevice.Name != null) && (dsDevice.Name.Length > 0))
                {
                    Camera camera = new Camera
                    {
                        Name = dsDevice.Name,
                        Path = $"video={dsDevice.Name}"
                    };
                    result.Add(camera);
                }
            }
        }
        else if (inputFormat == "avfoundation")
        {
            //result = SIPSorceryMedia.FFmpeg.Interop.MacOS.AvFoundation.GetCameraDevices();
        }
        else
        {
            AVInputFormat* avInputFormat = ffmpeg.av_find_input_format(inputFormat);
            AVDeviceInfoList* avDeviceInfoList = null;

            var error = ffmpeg.avdevice_list_input_sources(avInputFormat, null, null, &avDeviceInfoList);
            if (error < 0)
            {
                var bufferSize = 1024;
                var buffer = stackalloc byte[bufferSize];
                ffmpeg.av_strerror(error, buffer, (ulong)bufferSize);
                var message = Marshal.PtrToStringAnsi((IntPtr)buffer);
                throw new ApplicationException(message);
            }

            int nDevices = avDeviceInfoList->nb_devices;
            var avDevices = avDeviceInfoList->devices;

            result = new List<Camera>();
            for (int i = 0; i < nDevices; i++)
            {
                var avDevice = avDevices[i];
                var name = Marshal.PtrToStringAnsi((IntPtr)avDevice->device_description);
                var path = Marshal.PtrToStringAnsi((IntPtr)avDevice->device_name);

                if ((name != null) && (name.Length > 0))
                {
                    Camera camera = new Camera
                    {
                        Name = (name == null) ? "" : name,
                        Path = (path == null) ? "" : path,
                    };
                    result.Add(camera);
                }
            }

            ffmpeg.avdevice_free_list_devices(&avDeviceInfoList);
        }
        return result;
    }
}

public class Camera
{
    public string Name { get; set; }

    public string Path { get; set; }

    public Camera()
    {
        Name = Path = "";
    }
}
