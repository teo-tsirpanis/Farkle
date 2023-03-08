// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Text;

namespace Farkle.Grammars.GoldParser;

/// <summary>
/// Reads binary files with the same abstract format as GOLD Parser's grammar files.
/// </summary>
internal sealed class GrammarBinaryReader
{
    private const int MaxHeaderCharacterCount = 100;

    private const string UnexpectedEntryTypeMessage = "Unexpected entry type.";

    private readonly BinaryReader _reader;

    public GrammarBinaryReader(Stream input)
    {
        _reader = new BinaryReader(input);
        Header = ReadNullTerminatedString();
    }

    public string Header { get; }

    public int RemainingEntries { get; private set; }

    public bool NextRecord()
    {
        if (RemainingEntries != 0)
        {
            ThrowHelpers.ThrowInvalidDataException("Not all entries of the previous record have been read.");
        }

        try
        {
            if (_reader.ReadByte() != 'M')
            {
                ThrowHelpers.ThrowInvalidDataException("Unexpected record header");
            }
        }
        catch (EndOfStreamException)
        {
            return false;
        }
        RemainingEntries = _reader.ReadUInt16();
        return true;
    }

    private void ReadEntryType(char entryKind)
    {
        if (RemainingEntries == 0)
        {
            ThrowHelpers.ThrowInvalidDataException("Record is out of entries, call NextRecord() first.");
        }
        RemainingEntries--;

        if (_reader.ReadByte() != entryKind)
        {
            ThrowHelpers.ThrowInvalidDataException(UnexpectedEntryTypeMessage);
        }
    }

    private string ReadNullTerminatedString(bool isHeader = false)
    {
        StringBuilder sb = new StringBuilder(32);

        try
        {
            while (_reader.ReadUInt16() is ushort c && c != 0)
            {
                if (isHeader && sb.Length >= MaxHeaderCharacterCount)
                {
                    ThrowHelpers.ThrowInvalidDataException("Header is too long.");
                }
                sb.Append((char)c);
            }
        }
        catch (EndOfStreamException) when (isHeader)
        {
            // Return the incomplete header string.
        }

        return sb.ToString();
    }

    public byte ReadByte()
    {
        ReadEntryType('b');
        return _reader.ReadByte();
    }

    public bool ReadBoolean()
    {
        ReadEntryType('B');
        return _reader.ReadByte() is not 0;
    }

    public ushort ReadUInt16()
    {
        ReadEntryType('I');
        return _reader.ReadUInt16();
    }

    public string ReadString()
    {
        ReadEntryType('S');
        return ReadNullTerminatedString();
    }

    // When the standard specifies a reserved entry of type Empty, the most Lawful Good thing
    // to do is to read the entry without checking its type. Sure GOLD Parser is dead and it
    // won't ever be another thing, but still.
    public void SkipEntry()
    {
        switch ((char)_reader.ReadByte())
        {
            case 'E':
                break;
            case 'b' or 'B':
                _ = _reader.ReadByte();
                break;
            case 'I':
                _ = _reader.ReadUInt16();
                break;
            case 'S':
                _ = ReadNullTerminatedString();
                break;
            default:
                ThrowHelpers.ThrowInvalidDataException(UnexpectedEntryTypeMessage);
                break;
        }
    }
}
