using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HostManage.Helpers;

public static class VersionHelper
{
    private static readonly HttpClient HttpClient = new();

    public class UpdateInfo
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("changelog")]
        public string Changelog { get; set; } = string.Empty;

        [JsonPropertyName("releaseDate")]
        public DateTime? ReleaseDate { get; set; }

        public bool IsNewVersion { get; set; }
    }

    public static Version GetCurrentVersion()
    {
        try
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            var version = new Version(
                fileVersionInfo.FileMajorPart,
                fileVersionInfo.FileMinorPart,
                fileVersionInfo.FileBuildPart,
                fileVersionInfo.FilePrivatePart);
            return version;
        }
        catch (Exception)
        {
            return new Version(0, 0, 0, 0);
        }
    }

    public static async Task<UpdateInfo?> CheckUpdateAsync(string updateUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(updateUrl))
                return null;

            using var response = await HttpClient.GetAsync(updateUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                return null;

            string json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json, options);
            if (updateInfo == null)
                return null;

            var currentVersion = GetCurrentVersion();
            if (Version.TryParse(updateInfo.Version, out var latestVersion))
            {
                updateInfo.IsNewVersion = latestVersion > currentVersion;
            }

            return updateInfo;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task<bool> DownloadUpdateAsync(string url, string savePath, IProgress<double>? progress = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(savePath))
                return false;

            string? directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                return false;

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                if (totalBytes > 0 && progress != null)
                {
                    var percentage = (double)totalRead / totalBytes * 100;
                    progress.Report(percentage);
                }
            }

            progress?.Report(100);
            return totalRead > 0;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
