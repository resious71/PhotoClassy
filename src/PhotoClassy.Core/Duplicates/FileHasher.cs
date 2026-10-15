using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace PhotoClassy.Core.Duplicates
{
    public static class FileHasher
    {
        private const int BufferSize = 1024 * 1024;

        /// <summary>대용량 파일에 맞춰 1MB 버퍼로 순차 읽기하며 SHA256 을 계산한다.</summary>
        public static string ComputeSha256(string path, CancellationToken cancellationToken = default)
        {
            using (var sha = SHA256.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize,
                       FileOptions.SequentialScan))
            {
                var buffer = new byte[BufferSize];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    sha.TransformBlock(buffer, 0, read, null, 0);
                }
                sha.TransformFinalBlock(buffer, 0, 0);
                return ToHex(sha.Hash);
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                builder.Append(b.ToString("x2"));
            return builder.ToString();
        }
    }
}
