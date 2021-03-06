using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Maze.Sockets.Internal.Infrastructure;

namespace Maze.Sockets.Internal
{
    /// <summary>
    ///     A stream that queues buffers and reads from them linear
    /// </summary>
    public class BufferQueueStream : ReadOnlyStream
    {
        private readonly ArrayPool<byte> _bufferPool;
        private readonly Queue<ArraySegment<byte>> _buffers;
        private readonly AsyncAutoResetEvent _bufferWaitingAutoResetEvent;
        private readonly object _streamLock = new object();
        private int _currentBufferPosition;
        private bool _isDisposed;
        private long _length;
        private int _position;
        private bool _isCompleted;

        public BufferQueueStream(ArrayPool<byte> bufferPool)
        {
            _bufferPool = bufferPool;
            _buffers = new Queue<ArraySegment<byte>>();
            _bufferWaitingAutoResetEvent = new AsyncAutoResetEvent(false);
        }

        public override bool CanSeek { get; } = false;
        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (_isCompleted != value)
                {
                    _isCompleted = value;
                    _bufferWaitingAutoResetEvent.Set();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (_isDisposed)
                lock (_streamLock)
                {
                    if (!_isDisposed)
                    {
                        _isDisposed = true;
                        while (_buffers.Any())
                            DisposeCurrentBuffer();
                    }
                }
        }

        /// <summary>
        ///     Push a new buffer to the internal queue
        /// </summary>
        /// <param name="buffer">The buffer that should be enqueued</param>
        public void PushBuffer(ArraySegment<byte> buffer)
        {
            lock (_streamLock)
            {
                _buffers.Enqueue(buffer);
                _length += buffer.Count;
                _bufferWaitingAutoResetEvent.Set();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var currentPosition = 0;
            while (count > currentPosition)
            {
                lock (_streamLock)
                {
                    if (_buffers.Any())
                    {
                        CopyBuffer(buffer, offset + currentPosition, count - currentPosition, ref currentPosition);
                        if (currentPosition != count)
                            continue;
                    }

                    if (currentPosition > 0)
                        break;

                    if (IsCompleted)
                        return 0;
                }

                _bufferWaitingAutoResetEvent.Wait();
            }

            return currentPosition;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            var currentPosition = 0;
            while (count > currentPosition)
            {
                lock (_streamLock)
                {
                    if (_buffers.Any())
                    {
                        CopyBuffer(buffer, offset + currentPosition, count - currentPosition, ref currentPosition);
                        if (currentPosition != count)
                            continue;
                    }

                    if (currentPosition > 0)
                        break;

                    if (IsCompleted)
                        return 0;
                }

                await _bufferWaitingAutoResetEvent.WaitAsync(cancellationToken);
            }

            return currentPosition;
        }

        private void CopyBuffer(byte[] buffer, int offset, int count, ref int currentPosition)
        {
            var currentBuffer = _buffers.Peek();

            //either the count or the max amount of bytes left in the array
            var lengthToCopy = Math.Min(count, currentBuffer.Count - _currentBufferPosition);

            Buffer.BlockCopy(currentBuffer.Array, _currentBufferPosition + currentBuffer.Offset, buffer, offset,
                lengthToCopy);
            currentPosition += lengthToCopy;
            _currentBufferPosition += lengthToCopy;
            _position += lengthToCopy;

            if (_currentBufferPosition == currentBuffer.Count)
                DisposeCurrentBuffer();
        }

        private void DisposeCurrentBuffer()
        {
            var buffer = _buffers.Dequeue();
            _bufferPool?.Return(buffer.Array);
            _currentBufferPosition = 0;
        }

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}