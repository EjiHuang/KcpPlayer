namespace FFmpeg.Wrapper;

using System.Text;

public unsafe struct PacketSideDataList
{
    readonly AVPacketSideData** _entries;
    readonly int* _count;

    public AVPacketSideData* EntriesPtr => *_entries;
    public int Count => *_count;

    public PacketSideData this[int index] {
        get {
            if ((uint)index > (uint)Count) {
                throw new ArgumentOutOfRangeException();
            }
            return new(&EntriesPtr[index]);
        }
    }

    public PacketSideDataList(AVPacketSideData** entries, int* count)
    {
        _entries = entries;
        _count = count;
    }

    /// <summary> Returns the side data entry for the given type, or null if not present. </summary>
    public PacketSideData? Get(AVPacketSideDataType type)
    {
        AVPacketSideData* entry = ffmpeg.av_packet_side_data_get(*_entries, *_count, type);
        return entry != null ? new PacketSideData(entry) : null;
    }

    /// <summary> Allocates or overwrites a side data entry. </summary>
    public PacketSideData Add(AVPacketSideDataType type, int size)
    {
        var entry = ffmpeg.av_packet_side_data_new(_entries, _count, type, (ulong)size, 0);
        if (entry == null) {
            throw new OutOfMemoryException();
        }
        return new PacketSideData(entry);
    }

    public bool Remove(AVPacketSideDataType type)
    {
        int prevCount = Count;
        ffmpeg.av_packet_side_data_remove(*_entries, _count, type);
        return Count != prevCount;
    }

    public void Clear()
    {
        // https://github.com/FFmpeg/FFmpeg/blob/4e120fbbbd087c3acbad6ce2e8c7b1262a5c8632/libavfilter/f_sidedata.c#L117
        while (Count != 0) {
            ffmpeg.av_packet_side_data_remove(*_entries, _count, _entries[0]->type);
        }
    }

    /// <summary> Returns the value of an <see cref="AVPacketSideDataType.AV_PKT_DATA_DISPLAYMATRIX"/> entry. </summary>
    public int[]? GetDisplayMatrix()
    {
        var entry = Get(AVPacketSideDataType.AV_PKT_DATA_DISPLAYMATRIX);
        return entry?.GetDataRef<int_array9>().ToArray();
    }

    public override string ToString()
    {
        var sb = new StringBuilder("[");

        for (int i = 0; i < Count; i++) {
            if (i != 0) sb.Append(", ");
            sb.Append(this[i].ToString());
        }
        return sb.Append(']').ToString();
    }
}

public unsafe struct PacketSideData(AVPacketSideData* handle)
{
    public AVPacketSideData* Handle { get; } = handle;

    public Span<byte> Data => new Span<byte>(Handle->data, checked((int)Handle->size));
    public AVPacketSideDataType Type => Handle->type;

    /// <summary>
    /// Returns the side data payload reinterpreted as a <typeparamref name="T"/> pointer, 
    /// or null if the payload is smaller than <c>sizeof(T)</c>.
    /// </summary>
    public T* GetDataPtr<T>() where T : unmanaged
    {
        return Handle->size < (ulong)sizeof(T) ? null : (T*)Handle->data;
    }

    /// <summary>
    /// Returns the side data payload reinterpreted as a <typeparamref name="T"/> reference, 
    /// or throws <see cref="InvalidCastException"/> if the payload is smaller than <c>sizeof(T)</c>.
    /// </summary>
    public ref T GetDataRef<T>() where T : unmanaged
    {
        if (Handle->size < (ulong)sizeof(T)) {
            throw new InvalidCastException();
        }
        return ref *(T*)Handle->data;
    }

    public override string ToString() => $"{ffmpeg.av_packet_side_data_name(Type)}: {Handle->size} bytes";
}