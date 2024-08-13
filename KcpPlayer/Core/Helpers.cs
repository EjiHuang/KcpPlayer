﻿using Sdcb.FFmpeg.Raw;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace KcpPlayer.Core
{
    internal static unsafe class Helpers
    {
        public static unsafe string ErrorString(int errno)
        {
            byte* buf = stackalloc byte[ffmpeg.AV_ERROR_MAX_STRING_SIZE + 1];
            ffmpeg.av_strerror(errno, buf, ffmpeg.AV_ERROR_MAX_STRING_SIZE);
            return Marshal.PtrToStringAnsi((nint)buf)!;
        }
        public static int CheckError(this int errno)
        {
            if (errno < 0 && errno != ffmpeg.EAGAIN && errno != ffmpeg.AVERROR_EOF)
            {
                ThrowError(errno);
            }
            return errno;
        }
        public static int CheckError(this int errno, string msg)
        {
            if (errno < 0 && errno != ffmpeg.EAGAIN && errno != ffmpeg.AVERROR_EOF)
            {
                ThrowError(errno, msg);
            }
            return errno;
        }
        public static Exception ThrowError(this int errno, string? msg = null)
        {
            msg ??= "Operation failed";
            throw new InvalidOperationException(msg + ": " + ErrorString(errno));
        }

        public static ReadOnlySpan<T> GetSpanFromSentinelTerminatedPtr<T>(T* ptr, T terminator) where T : unmanaged
        {
            int len = 0;

            while (ptr != null && !ptr[len].Equals(terminator))
            {
                len++;
            }
            return new ReadOnlySpan<T>(ptr, len);
        }

        public static string? PtrToStringUTF8(byte* ptr)
        {
            if (ptr == null)
            {
                return null;
            }
            var span = new Span<byte>(ptr, int.MaxValue);
            int length = span.IndexOf((byte)0);
            return Encoding.UTF8.GetString(ptr, length);
        }

        public static long? GetPTS(long pts) => pts != ffmpeg.AV_NOPTS_VALUE ? pts : null;
        public static void SetPTS(ref long pts, long? value) => pts = value ?? ffmpeg.AV_NOPTS_VALUE;

        public static TimeSpan? GetTimeSpan(long pts, Rational timeBase)
        {
            if (pts == ffmpeg.AV_NOPTS_VALUE)
            {
                return null;
            }
            return Rational.GetTimeSpan(pts, timeBase);
        }
    }
}
