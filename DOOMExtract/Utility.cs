using System.IO;

namespace DOOMExtract
{
    public static class Utility
    {
        public static long StreamCopy(Stream destStream, Stream sourceStream, int bufferSize, long length)
        {
            long read = 0;
            while (read < length)
            {
                int toRead = bufferSize;
                if (toRead > length - read)
                    toRead = (int)(length - read);

                var buf = new byte[toRead];
                int buf_read = sourceStream.Read(buf, 0, toRead);
                destStream.Write(buf, 0, buf_read);
                read += buf_read;
                if (buf_read == 0)
                    break; // no more to be read..
            }
            return read;
        }
    }
}
