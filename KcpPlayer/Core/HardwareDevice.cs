using Sdcb.FFmpeg.Raw;

namespace KcpPlayer.Core
{
    public unsafe class HardwareDevice : FFObject
    {
        private AVBufferRef* _ctx;

        public AVBufferRef* Handle
        {
            get
            {
                ThrowIfDisposed();
                return _ctx;
            }
        }
        public AVHWDeviceContext* RawHandle
        {
            get
            {
                ThrowIfDisposed();
                return (AVHWDeviceContext*)_ctx->data;
            }
        }

        public AVHWDeviceType Type => RawHandle->type;

        public HardwareDevice(AVBufferRef* deviceCtx)
        {
            _ctx = deviceCtx;
        }

        /// <summary> Open a device of the specified type and create a context for it. </summary>
        /// <returns> The created device context or null on failure. </returns>
        public static HardwareDevice? Create(AVHWDeviceType type)
        {
            AVBufferRef* ctx;
            if (ffmpeg.av_hwdevice_ctx_create(&ctx, type, null, null, 0) < 0)
            {
                return null;
            }
            return new HardwareDevice(ctx);
        }

        /// <inheritdoc cref="ffmpeg.av_hwdevice_get_hwframe_constraints(AVBufferRef*, void*)"/>
        public HardwareFrameConstraints? GetMaxFrameConstraints()
        {
            var desc = ffmpeg.av_hwdevice_get_hwframe_constraints(_ctx, null);

            if (desc == null)
            {
                return null;
            }
            var managedDesc = new HardwareFrameConstraints(desc);
            ffmpeg.av_hwframe_constraints_free(&desc);
            return managedDesc;
        }

        /// <param name="swFormat"> The pixel format identifying the actual data layout of the hardware frames. </param>
        /// <param name="initialSize"> Initial size of the frame pool. If a device type does not support dynamically resizing the pool, then this is also the maximum pool size. </param>
        public HardwareFramePool? CreateFramePool(int width, int height, AVPixelFormat swFormat, int initialSize)
        {
            ThrowIfDisposed();

            var poolRef = ffmpeg.av_hwframe_ctx_alloc(_ctx);
            if (poolRef == null)
            {
                throw new OutOfMemoryException("Failed to allocate hardware frame pool");
            }
            var pool = (AVHWFramesContext*)poolRef->data;
            pool->format = GetDefaultSurfaceFormat();
            pool->sw_format = swFormat;
            pool->width = width;
            pool->height = height;
            pool->initial_pool_size = initialSize;

            if (ffmpeg.av_hwframe_ctx_init(poolRef) < 0)
            {
                ffmpeg.av_buffer_unref(&poolRef);
                return null;
            }
            return new HardwareFramePool(poolRef);
        }

        private AVPixelFormat GetDefaultSurfaceFormat()
        {
            return Type switch
            {
                AVHWDeviceType.Vdpau => AVPixelFormat.Vdpau,
                AVHWDeviceType.Cuda => AVPixelFormat.Cuda,
                AVHWDeviceType.Vaapi => AVPixelFormat.Vaapi,
                AVHWDeviceType.Dxva2 => AVPixelFormat.Dxva2Vld,
                AVHWDeviceType.Qsv => AVPixelFormat.Qsv,
                AVHWDeviceType.D3d11va => AVPixelFormat.D3d11,
                AVHWDeviceType.Drm => AVPixelFormat.DrmPrime,
                AVHWDeviceType.Opencl => AVPixelFormat.Opencl,
                AVHWDeviceType.Vulkan => AVPixelFormat.Vulkan,
                AVHWDeviceType.Videotoolbox => AVPixelFormat.Videotoolbox,
                AVHWDeviceType.Mediacodec => AVPixelFormat.Mediacodec
            };
        }

        protected override void Free()
        {
            if (_ctx != null)
            {
                fixed (AVBufferRef** ppCtx = &_ctx)
                {
                    ffmpeg.av_buffer_unref(ppCtx);
                }
            }
        }
        private void ThrowIfDisposed()
        {
            if (_ctx == null)
            {
                throw new ObjectDisposedException(nameof(HardwareDevice));
            }
        }
    }
}
