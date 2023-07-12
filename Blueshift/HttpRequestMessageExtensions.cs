namespace Blueshift
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;

    public static class HttpRequestMessageExtensions
    {
        public static async Task<HttpRequestMessage> Clone(this HttpRequestMessage request)
        {
            HttpRequestMessage newRequest = new(request.Method, request.RequestUri);

            // Copy the request's content (via a MemoryStream) into the cloned object. Note that the MemoryStream
            // is not disposed here because it needs to be assigned to the new request (and will be disposed of 
            // after the request has been sent.
            if (request.Content != null)
            {
                MemoryStream memoryStream = new();

                // Copy the content from the original request. Note that the HttpClient normally disposes of the
                // content stream upon sending. The DelayedDispose*Content classes prevent the immediate disposing
                // of the underlying stream, allowing it to be re-read here.
                await request.Content.CopyToAsync(memoryStream).ConfigureAwait(false);
                memoryStream.Position = 0;
                newRequest.Content = new StreamContent(memoryStream);

                // Copy the content headers
                if (request.Content.Headers != null)
                {
                    foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
                    {
                        newRequest.Content.Headers.Add(header.Key, header.Value);
                    }
                }
            }

            newRequest.Version = request.Version;

            foreach (KeyValuePair<string, object> option in request.Options)
            {
                newRequest.Options.Set(new HttpRequestOptionsKey<string>(option.Key), (string)option.Value);
            }

            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
            {
                newRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return newRequest;
        }

        public static void DisposeCustomContent(this HttpRequestMessage request)
        {
            try
            {
                IDelayedDisposeContent delayedDisposeContent = request.Content as IDelayedDisposeContent;
                delayedDisposeContent?.DelayedDispose();
            }
            catch (ObjectDisposedException)
            {
                // Suppress object disposed exception for cases where the request or the request's content 
                // has already been disposed.
            }
        }
    }

    public interface IDelayedDisposeContent
    {
        void DelayedDispose();
    }

    public class DelayedDisposeStringContent : StringContent, IDelayedDisposeContent
    {
        public DelayedDisposeStringContent(string content)
            : base(content)
        {
        }

        public DelayedDisposeStringContent(string content, Encoding encoding)
            : base(content, encoding)
        {
        }

        public DelayedDisposeStringContent(string content, Encoding encoding, string mediaType)
            : base(content, encoding, mediaType)
        {
        }

        protected override void Dispose(bool disposing)
        {
            // Do not dispose of resources normally
        }

        public void DelayedDispose()
        {
            base.Dispose(true);
        }
    }

    public class DelayedDisposeStreamContent : StreamContent, IDelayedDisposeContent
    {
        public DelayedDisposeStreamContent(Stream content)
            : base(content)
        {
        }

        public DelayedDisposeStreamContent(Stream content, int bufferSize)
            : base(content, bufferSize)
        {
        }

        protected override void Dispose(bool disposing)
        {
            // Do not dispose of resources normally
        }

        public void DelayedDispose()
        {
            base.Dispose(true);
        }
    }
}
