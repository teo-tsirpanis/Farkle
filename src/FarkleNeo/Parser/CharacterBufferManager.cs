// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Buffers;

namespace Farkle.Parser;

internal struct CharacterBufferManager<TChar>
{
    private TChar[] _buffer;
    private readonly int _initialBufferSize;
    private long _totalCharactersConsumed = 0;
    private int _usedCharacterStart = 0, _usedCharacterEnd = 0;

    public CharacterBufferManager(int initialBufferSize)
    {
        _initialBufferSize = initialBufferSize;
        _buffer = ArrayPool<TChar>.Shared.Rent(initialBufferSize);
    }

    private readonly void CheckInputNotCompleted()
    {
        if (IsInputCompleted)
        {
            throw new InvalidOperationException("Input is completed.");
        }
    }

    private void ExpandBuffer(int minRequiredCharacters)
    {
        int totalAvailableCharacters = _buffer.Length - _usedCharacterEnd + _usedCharacterStart;
        if (totalAvailableCharacters > 0 && totalAvailableCharacters >= minRequiredCharacters)
        {
            Array.Copy(_buffer, _usedCharacterStart, _buffer, 0, _usedCharacterEnd - _usedCharacterStart);
            _usedCharacterEnd -= _usedCharacterStart;
            _usedCharacterStart = 0;
            return;
        }
        int newSize = Math.Max(_buffer.Length * 2, _buffer.Length + minRequiredCharacters);
        TChar[] newBuffer = ArrayPool<TChar>.Shared.Rent(newSize);
        _buffer.CopyTo(newBuffer, _usedCharacterStart);
        ArrayPool<TChar>.Shared.Return(_buffer);
        _buffer = newBuffer;
        _usedCharacterEnd -= _usedCharacterStart;
        _usedCharacterStart = 0;
    }

    private void EnsureWriteBufferSize(int sizeHint)
    {
        CheckInputNotCompleted();
        sizeHint = Math.Max(sizeHint, 0);

        // Check how many characters we can immediately hand out.
        int writeBufferSize = _buffer.Length - _usedCharacterEnd;

        // If we have characters and they are more than the size hint, return them.
        if (writeBufferSize == 0 || writeBufferSize < sizeHint)
        {
            ExpandBuffer(sizeHint);
        }
    }

    public bool IsInputCompleted { get; private set; }

    public readonly ReadOnlySpan<TChar> UsedCharacters =>
        _buffer.AsSpan(_usedCharacterStart, _usedCharacterEnd - _usedCharacterStart);

    public void Advance(int count)
    {
        CheckInputNotCompleted();
        if ((uint)count > (uint)(_buffer.Length - _usedCharacterEnd))
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        _usedCharacterEnd += count;
    }

    public void CompleteInput()
    {
        CheckInputNotCompleted();
        IsInputCompleted = true;
    }

    public Span<TChar> GetSpan(int sizeHint)
    {
        EnsureWriteBufferSize(sizeHint);
        return _buffer.AsSpan(_usedCharacterEnd);
    }

    public Memory<TChar> GetMemory(int sizeHint)
    {
        EnsureWriteBufferSize(sizeHint);
        return _buffer.AsMemory(_usedCharacterEnd);
    }

#if !(NETCOREAPP || NETSTANDARD2_1_OR_GREATER)
    public ArraySegment<TChar> GetArraySegment(int sizeHint)
    {
        EnsureWriteBufferSize(sizeHint);
        return new ArraySegment<TChar>(_buffer, _usedCharacterEnd, _buffer.Length - _usedCharacterEnd);
    }
#endif

    public void Reset()
    {
        if (_buffer is not [])
        {
            ArrayPool<TChar>.Shared.Return(_buffer);
        }
        _buffer = ArrayPool<TChar>.Shared.Rent(_initialBufferSize);
        _totalCharactersConsumed = 0;
        _usedCharacterStart = 0;
        _usedCharacterEnd = 0;
        IsInputCompleted = false;
    }

    /// <summary>
    /// Updates the buffer manager's state after the parser has been invoked.
    /// </summary>
    /// <param name="totalCharactersConsumed">The value of <see cref="ParserState.TotalCharactersConsumed"/>.</param>
    /// <param name="isCompleted">The value of <see cref="ParserCompletionState{T}.IsCompleted"/>.</param>
    public void UpdateStateFromParser(long totalCharactersConsumed, bool isCompleted)
    {
        long newCharactersConsumed = totalCharactersConsumed - _totalCharactersConsumed;
        if ((ulong)newCharactersConsumed > (ulong)_usedCharacterEnd)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCharactersConsumed));
        }
        _totalCharactersConsumed = totalCharactersConsumed;
        _usedCharacterStart += (int)newCharactersConsumed;

        if (isCompleted || (_usedCharacterStart == _usedCharacterEnd && IsInputCompleted))
        {
            ArrayPool<TChar>.Shared.Return(_buffer);
            _buffer = Array.Empty<TChar>();
            IsInputCompleted = true;
        }
    }
}
