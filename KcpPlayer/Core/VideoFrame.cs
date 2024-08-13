using Sdcb.FFmpeg.Raw;

namespace KcpPlayer.Core
{
    public unsafe class VideoFrame : MediaFrame
    {
        public int Width => _frame->width;
        public int Height => _frame->height;
        public AVPixelFormat PixelFormat => (AVPixelFormat)_frame->format;

        /// <summary> Pointers to the pixel data planes. </summary>
        /// <remarks> These can point to the end of image data when used in combination with negative values in <see cref="RowSize"/>. </remarks>
        public byte** Data => (byte**)&_frame->data;

        /// <summary> An array of positive or negative values indicating the size in bytes of each pixel row. </summary>
        /// <remarks> 
        /// - Values may be larger than the size of usable data -- there may be extra padding present for performance reasons. <br/>
        /// - Values can be negative to achieve a vertically inverted iteration over image rows.
        /// </remarks>
        public int* RowSize => (int*)&_frame->linesize;

        /// <summary> Whether this frame is attached to a hardware frame context. </summary>
        public bool IsHardwareFrame => _frame->hw_frames_ctx != null;

        /// <summary> Whether the frame rows are flipped. Alias for <c>RowSize[0] &lt; 0</c>. </summary>
        public bool IsVerticallyFlipped => _frame->linesize[0] < 0;

        /// <summary> Wraps an existing <see cref="AVFrame"/> pointer. </summary>
        /// <param name="takeOwnership">True if <paramref name="frame"/> should be freed when Dispose() is called.</param>
        public VideoFrame(AVFrame* frame, bool takeOwnership)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }
            _frame = frame;
            _ownsFrame = takeOwnership;
        }

        /// <summary> Returns a view over the pixel row for the specified plane. </summary>
        /// <remarks> The returned span may be longer than <see cref="Width"/> due to padding. </remarks>
        /// <param name="y">Row index, in top to bottom order.</param>
        public Span<T> GetRowSpan<T>(int y, int plane = 0) where T : unmanaged
        {
            if ((uint)y >= (uint)GetPlaneSize(plane).Height)
            {
                throw new ArgumentOutOfRangeException();
            }
            int stride = RowSize[plane];
            return new Span<T>(&Data[plane][y * stride], Math.Abs(stride / sizeof(T)));
        }

        /// <summary> Returns a view over the pixel data for the specified plane. </summary>
        /// <remarks> Note that rows may be stored in reverse order depending on <see cref="IsVerticallyFlipped"/>. </remarks>
        /// <param name="stride">Number of pixels per row.</param>
        public Span<T> GetPlaneSpan<T>(int plane, out int stride) where T : unmanaged
        {
            int height = GetPlaneSize(plane).Height;

            byte* data = (byte*)_frame->data[plane];
            int rowSize = _frame->linesize[plane];

            if (rowSize < 0)
            {
                data += rowSize * (height - 1);
                rowSize *= -1;
            }
            stride = rowSize / sizeof(T);
            return new Span<T>(data, checked(height * stride));
        }

        public (int Width, int Height) GetPlaneSize(int plane)
        {
            ThrowIfDisposed();

            var size = (Width, Height);

            //https://github.com/FFmpeg/FFmpeg/blob/c558fcf41e2027a1096d00b286954da2cc4ae73f/libavutil/imgutils.c#L111
            if (plane == 0)
            {
                return size;
            }
            var desc = ffmpeg.av_pix_fmt_desc_get(PixelFormat);

            if (desc == null || (desc->flags & (int)AV_PIX_FMT_FLAG.Hwaccel) != 0)
            {
                throw new InvalidOperationException();
            }
            for (int i = 0; i < 4; i++)
            {
                if (desc->comp[i].plane != plane) continue;

                if ((i == 1 || i == 2) && (desc->flags & (int)AV_PIX_FMT_FLAG.Rgb) == 0)
                {
                    size.Width = CeilShr(size.Width, desc->log2_chroma_w);
                    size.Height = CeilShr(size.Height, desc->log2_chroma_h);
                }
                return size;
            }
            throw new ArgumentOutOfRangeException(nameof(plane));

            static int CeilShr(int x, int s) => (x + (1 << s) - 1) >> s;
        }

        /// <summary> Attempts to create a hardware frame memory mapping. Returns null if the backing device does not support frame mappings. </summary>
        public VideoFrame? Map(HardwareFrameMappingFlags flags)
        {
            ThrowIfDisposed();
            if (!IsHardwareFrame)
            {
                throw new InvalidOperationException("Cannot create mapping of non-hardware frame.");
            }

            var mapping = ffmpeg.av_frame_alloc();
            int result = ffmpeg.av_hwframe_map(mapping, _frame, (int)flags);

            if (result == 0)
            {
                mapping->width = _frame->width;
                mapping->height = _frame->height;
                return new VideoFrame(mapping, takeOwnership: true);
            }
            ffmpeg.av_frame_free(&mapping);
            return null;
        }
    }

    /// <summary> Flags to apply to hardware frame memory mappings. </summary>
    public enum HardwareFrameMappingFlags
    {
        /// <summary> The mapping must be readable. </summary>
        Read = 1 << 0,
        /// <summary> The mapping must be writeable. </summary>
        Write = 1 << 1,
        /// <summary>
        /// The mapped frame will be overwritten completely in subsequent
        /// operations, so the current frame data need not be loaded.  Any values
        /// which are not overwritten are unspecified.
        /// </summary>
        Overwrite = 1 << 2,
        /// <summary>
        /// The mapping must be direct.  That is, there must not be any copying in
        /// the map or unmap steps.  Note that performance of direct mappings may
        /// be much lower than normal memory.
        /// </summary>
        Direct = 1 << 3,
    }
    public enum HardwareFrameTransferDirection
    {
        /// <summary> Transfer the data from the queried hw frame. </summary>
        From = AVHWFrameTransferDirection.From,
        /// <summary> Transfer the data to the queried hw frame. </summary>
        To = AVHWFrameTransferDirection.To,
    }
}
