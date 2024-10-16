using System.Collections.Concurrent;
using System.Diagnostics;

namespace FFmpeg.Wrapper;

internal class QueueIOContext : IOContext
{
    readonly ConcurrentQueue<byte[]> _queue;
    readonly bool _leaveOpen;

    byte[]? _buffer;
    readonly byte[] _scratchBuffer = new byte[4096 * 4];
    int _start = 0;

    public QueueIOContext(ConcurrentQueue<byte[]> queue, bool leaveOpen, int bufferSize)
        : base(bufferSize, canRead: true, canWrite: false, canSeek: true)
    {
        _queue = queue;
        _leaveOpen = leaveOpen;
    }

    protected override int Read(Span<byte> buffer)
    {
        if (_buffer != null && _start >= _buffer.Length)
        {
            _start = 0;
            _buffer = null;
        }

        if (_start == 0 && _queue.TryDequeue(out var temp))
        {
            if (temp.Length > buffer.Length)
            {
                var length = buffer.Length;
                temp.AsSpan(_start, length).CopyTo(buffer);
                _buffer = temp;
                _start += length;
                return length;
            }
            else
            {
                temp.AsSpan(0, temp.Length).CopyTo(buffer);
                _start = 0;
                return temp.Length;
            }
        }
        else
        {
            if (_buffer != null && _start < _buffer.Length)
            {
                var length = Math.Min(_buffer.Length - _start, buffer.Length);
                _buffer.AsSpan(_start, length).CopyTo(buffer);
                _start += length;
                return length;
            }
        }

        return 0;
    }

    protected override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    protected override void Write(ReadOnlySpan<byte> buffer)
    {
        throw new NotImplementedException();
    }

    protected override long? GetLength()
    {
        try
        {
            return _queue.Count;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    protected override void Free()
    {
        base.Free();

        if (!_leaveOpen)
        {
            while (_queue.TryDequeue(out var _)) { }
        }
    }
}
