using System;
using System.Drawing;
using System.IO;

namespace DOOMExtract
{
    public class EndianWriter : BinaryWriter
    {
        private bool _bigEndian;

        internal EndianIO _io;

        public bool BigEndian
        {
            get { return _io != null ? _io.BigEndian : _bigEndian; }
            set
            {
                if (_io != null) { _io.BigEndian = value; }
                else
                {
                    _bigEndian = value;
                }
            }
        }

        internal EndianWriter(EndianIO _io)
            : base(_io.Stream)
        {
            this._io = _io;
        }

        public EndianWriter(Stream stream)
            : base(stream)
        {

        }

        public void SeekTo(int offset)
        {
            SeekTo(offset, SeekOrigin.Begin);
        }

        public void SeekTo(long offset)
        {
            SeekTo((int)offset, SeekOrigin.Begin);
        }

        public void SeekTo(uint offset)
        {
            SeekTo((int)offset, SeekOrigin.Begin);
        }

        public void SeekTo(int offset, SeekOrigin seekOrigin)
        {
            BaseStream.Seek(offset, seekOrigin);
        }

        public override void Write(string value)
        {
            int length = value.Length;
            for (int i = 0; i < length; i++)
            {
                byte num3 = (byte)value[i];
                Write(num3);
            }
        }

        public override void Write(double value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BigEndian)
            {
                Array.Reverse(bytes);
            }
            base.Write(bytes);
        }

        public override void Write(short value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BigEndian)
            {
                Array.Reverse(bytes);
            }
            base.Write(bytes);
        }

        public override void Write(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BigEndian)
            {
                Array.Reverse(bytes);
            }
            base.Write(bytes);
        }

        public void WriteInt24(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            byte num = bytes[0];
            byte num2 = bytes[1];
            byte num3 = bytes[2];
            byte[] array = new[] { num, num2, num3 };
            if (BigEndian)
            {
                Array.Reverse(array);
            }
            Write(array);
        }

        public override void Write(long value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BigEndian)
            {
                Array.Reverse(bytes);
            }
            base.Write(bytes);
        }

        public override void Write(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BigEndian)
            {
                Array.Reverse(bytes);
            }
            base.Write(bytes);
        }
        public override void Write(ushort value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BigEndian)
            {
                Array.Reverse(bytes);
            }
            base.Write(bytes);
        }

        public override void Write(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BigEndian)
            {
                Array.Reverse(bytes);
            }
            base.Write(bytes);
        }

        public override void Write(ulong value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BigEndian)
            {
                Array.Reverse(bytes);
            }
            base.Write(bytes);
        }

        public void WriteAsciiString(string value, int length)
        {
            int length1 = value.Length;
            for (int i = 0; i < length1; i++)
            {
                if (i > length)
                {
                    break;
                }
                byte num3 = (byte)value[i];
                Write(num3);
            }
            int num4 = length - length1;
            if (num4 > 0)
            {
                Write(new byte[num4]);
            }
        }

        public void WriteNullTerminatedUnicodeString(string value)
        {
            int strLen = value.Length;
            for (int x = 0; x < strLen; x++)
            {
                ushort val = value[x];
                Write(val);
            }
            Write((ushort)0);
        }

        public void WriteNullTerminatedAsciiString(string value)
        {
            Write(value.ToCharArray());
            Write((byte)0);
        }

        public void WriteUnicodeString(string value, int length)
        {
            int length1 = value.Length;
            for (int i = 0; i < length1; i++)
            {
                if (i > length)
                {
                    break;
                }
                ushort num3 = value[i];
                Write(num3);
            }
            int num4 = (length - length1) * 2;
            if (num4 > 0)
            {
                Write(new byte[num4]);
            }
        }
    }

    public class EndianReader : BinaryReader
    {
        internal EndianIO _io;

        public bool BigEndian
        {
            get { return _io.BigEndian; }
            set { _io.BigEndian = value; }
        }

        internal EndianReader(EndianIO io)
            : base(io.Stream)
        {
            _io = io;
        }

        public string ReadAsciiString(int length)
        {
            string str = "";
            int num = 0;
            for (int i = 0; i < length; i++)
            {
                char ch = ReadChar();
                num++;
                if (ch == '\0')
                {
                    break;
                }
                str = str + ch;
            }
            int num3 = length - num;
            BaseStream.Seek(num3, SeekOrigin.Current);
            return str;
        }

        public override double ReadDouble()
        {
            byte[] array = base.ReadBytes(4);
            if (BigEndian)
            {
                Array.Reverse(array);
            }
            return BitConverter.ToDouble(array, 0);
        }

        public override short ReadInt16()
        {
            byte[] array = base.ReadBytes(2);
            if (BigEndian)
            {
                Array.Reverse(array);
            }
            return BitConverter.ToInt16(array, 0);
        }

        public int ReadInt24()
        {
            byte[] sourceArray = base.ReadBytes(3);
            byte[] destinationArray = new byte[4];
            Array.Copy(sourceArray, 0, destinationArray, 0, 3);
            if (BigEndian)
            {
                Array.Reverse(destinationArray);
            }
            return BitConverter.ToInt32(destinationArray, 0);
        }

        public override int ReadInt32()
        {
            byte[] array = base.ReadBytes(4);
            if (BigEndian)
            {
                Array.Reverse(array);
            }
            return BitConverter.ToInt32(array, 0);
        }

        public override long ReadInt64()
        {
            byte[] array = base.ReadBytes(8);
            if (BigEndian)
            {
                Array.Reverse(array);
            }
            return BitConverter.ToInt64(array, 0);
        }

        public string ReadNullTerminatedAsciiString()
        {
            string newString = string.Empty;
            while (true)
            {
                byte tempChar = ReadByte();
                if (tempChar != 0)
                    newString += (char)tempChar;
                else
                    break;
            }
            return newString;
        }

        public string ReadNullTerminatedUnicodeString()
        {
            string newString = string.Empty;
            while (true)
            {
                ushort tempChar = ReadUInt16();
                if (tempChar != 0)
                    newString += (char)tempChar;
                else
                    break;
            }
            return newString;
        }

        public override float ReadSingle()
        {
            byte[] array = base.ReadBytes(4);
            if (BigEndian)
            {
                Array.Reverse(array);
            }
            return BitConverter.ToSingle(array, 0);
        }

        public string ReadString(int length)
        {
            return ReadAsciiString(length);
        }

        public override ushort ReadUInt16()
        {
            byte[] array = base.ReadBytes(2);
            if (BigEndian)
            {
                Array.Reverse(array);
            }
            return BitConverter.ToUInt16(array, 0);
        }

        public override uint ReadUInt32()
        {
            byte[] array = base.ReadBytes(4);
            if (BigEndian)
            {
                Array.Reverse(array);
            }
            return BitConverter.ToUInt32(array, 0);
        }

        public override ulong ReadUInt64()
        {
            byte[] array = base.ReadBytes(8);
            if (BigEndian)
            {
                Array.Reverse(array);
            }
            return BitConverter.ToUInt64(array, 0);
        }

        public string ReadUnicodeString(int length)
        {
            string str = "";
            int num = 0;
            for (int i = 0; i < length; i++)
            {
                char ch = (char)ReadUInt16();
                num++;
                if (ch == '\0')
                {
                    break;
                }
                str = str + ch;
            }
            int num3 = (length - num) * 2;
            BaseStream.Seek(num3, SeekOrigin.Current);
            return str;
        }

        public void SeekTo(int offset)
        {
            SeekTo(offset, SeekOrigin.Begin);
        }

        public void SeekTo(long offset)
        {
            SeekTo((int)offset, SeekOrigin.Begin);
        }

        public override string ReadString()
        {
            return ReadNullTerminatedAsciiString();
        }

        public void SeekTo(uint offset)
        {
            SeekTo((int)offset, SeekOrigin.Begin);
        }

        public void SeekTo(int offset, SeekOrigin seekOrigin)
        {
            BaseStream.Seek(offset, seekOrigin);
        }
    }

    public class EndianIO
    {
        public EndianReader Reader;
        public EndianWriter Writer;

        public string FilePath;

        public Stream Stream;

        public bool BigEndian;

        public long Length
        {
            get
            {
                return Stream.Length;
            }
        }

        public EndianIO(string filePath, FileMode fileMode, bool bigEndian)
        {
            Stream = new FileStream(filePath, fileMode);
            FilePath = filePath;
            BigEndian = bigEndian;
            Open();
        }

        public EndianIO(Stream stream, bool bigEndian)
        {
            Stream = stream;
            BigEndian = bigEndian;
            Open();
        }

        public EndianIO(string filePath, FileMode fileMode)
        {
            FilePath = filePath;
            Stream = new FileStream(filePath, fileMode);
            Open();
        }

        public EndianIO(Stream stream)
        {
            Stream = stream;
            Open();
        }

        public EndianIO(byte[] data)
        {
            Stream = new MemoryStream(data);
            Open();
        }
        public EndianIO(byte[] data, bool bigEndian)
        {
            Stream = new MemoryStream(data);
            BigEndian = bigEndian;
            Open();
        }

        public void Open()
        {
            Reader = new EndianReader(this);
            Writer = new EndianWriter(this);
        }

        public void Close()
        {
            try
            {
                Stream.Close();
                Stream = null;

            }
            catch
            {
            }
        }
    }
}