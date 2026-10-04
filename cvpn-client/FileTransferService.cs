using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CustomVPN.Client
{
    public class FileTransferService
    {
        public const int TransferPort = 47800;
        private static TcpListener? _listener;
        private static CancellationTokenSource? _cts;

        public static event Action<string, string, long>? FileReceived;
        public static event Action<string, int>? TransferProgress;

        public static void StartListener()
        {
            if (_listener != null) return;

            try
            {
                _cts = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Any, TransferPort);
                _listener.Start();

                Task.Run(() => ListenLoop(_cts.Token));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start file listener: {ex.Message}");
            }
        }

        public static void StopListener()
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener = null;
        }

        private static async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(token);
                    _ = Task.Run(() => HandleIncomingFile(client));
                }
                catch
                {
                    break;
                }
            }
        }

        private static async Task HandleIncomingFile(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new BinaryReader(stream))
            {
                try
                {
                    // Read header: filename length + filename + file size
                    var fileName = reader.ReadString();
                    var fileSize = reader.ReadInt64();

                    var downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "CustomVPN");
                    Directory.CreateDirectory(downloadsFolder);
                    var destinationPath = Path.Combine(downloadsFolder, Path.GetFileName(fileName));

                    using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                    {
                        byte[] buffer = new byte[64 * 1024];
                        long totalRead = 0;

                        while (totalRead < fileSize)
                        {
                            int toRead = (int)Math.Min(buffer.Length, fileSize - totalRead);
                            int read = await stream.ReadAsync(buffer, 0, toRead);
                            if (read == 0) break;

                            await fileStream.WriteAsync(buffer, 0, read);
                            totalRead += read;
                        }
                    }

                    FileReceived?.Invoke(fileName, destinationPath, fileSize);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error receiving file: {ex.Message}");
                }
            }
        }

        public static async Task<bool> SendFileAsync(string targetIp, string filePath)
        {
            if (!File.Exists(filePath)) return false;

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Parse(targetIp), TransferPort);

                using var stream = client.GetStream();
                using var writer = new BinaryWriter(stream);
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

                var fileInfo = new FileInfo(filePath);
                writer.Write(fileInfo.Name);
                writer.Write(fileInfo.Length);

                byte[] buffer = new byte[64 * 1024];
                long totalSent = 0;
                int read;

                while ((read = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await stream.WriteAsync(buffer, 0, read);
                    totalSent += read;

                    int percent = (int)((totalSent * 100) / fileInfo.Length);
                    TransferProgress?.Invoke(fileInfo.Name, percent);
                }

                await stream.FlushAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send file: {ex.Message}");
                return false;
            }
        }
    }
}
