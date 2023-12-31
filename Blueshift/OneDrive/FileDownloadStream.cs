﻿namespace Blueshift.OneDrive
{
    internal class FileDownloadStream : Stream
    {
        private readonly OneDriveClient client;
        private readonly Uri downloadUri;

        private const int FragmentLength = 10485760;

        // Initialize to an empty array will trigger a download on the first read.
        private byte[] currentBuffer = new byte[0];

        // The offset in the current buffer where the next byte should be read from
        private int currentBufferOffset;

        // The next fragment to request
        private int currentFragmentOffset;

        private bool isFinalBuffer;

        public FileDownloadStream(OneDriveClient client, Uri downloadUri)
        {
            this.client = client;
            this.downloadUri = downloadUri;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                // Check if we need to download a fragment
                if (this.currentBufferOffset >= this.currentBuffer.Length)
                {
                    if (this.isFinalBuffer)
                    {
                        return i;
                    }

                    HttpResponseMessage response = this.client.DownloadFileFragment(
                        this.downloadUri,
                        this.currentFragmentOffset,
                        FragmentLength).Result;

                    using (response)
                    {
                        this.currentBuffer = response.Content.ReadAsByteArrayAsync().Result;

                        this.currentFragmentOffset++;
                        this.currentBufferOffset = 0;

                        var rangeHeader = response.Content.Headers.ContentRange;
                        Pre.Assert(rangeHeader.To != null, "rangeHeader.To != null");
                        Pre.Assert(rangeHeader.Length != null, "rangeHeader.Length != null");

                        if (rangeHeader.To.Value == rangeHeader.Length - 1)
                        {
                            this.isFinalBuffer = true;
                        }
                    }
                }

                buffer[i + offset] = this.currentBuffer[this.currentBufferOffset];

                this.currentBufferOffset++;
            }

            return count;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }
    }

    public class TokenRefreshedEventArgs : EventArgs
    {
        public TokenResponse NewToken { get; set; }
    }
}
