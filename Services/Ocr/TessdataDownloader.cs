using System.IO;
using System.Net.Http;
using ScreenTranslator.Services;

namespace ScreenTranslator.Services.Ocr;

public sealed class TessdataDownloader
{
    private const string BaseUrl = "https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main";

    private readonly HttpClient _http;

    public TessdataDownloader(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> EnsureAsync(string tessdataFolder, string tesseractLanguage, CancellationToken token = default)
    {
        Directory.CreateDirectory(tessdataFolder);

        var path = Path.Combine(tessdataFolder, $"{tesseractLanguage}.traineddata");

        if (File.Exists(path))
            return path;

        var url = $"{BaseUrl}/{tesseractLanguage}.traineddata";

        Logger.Write($"Downloading OCR language data: {url}");

        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"No OCR language data found for \"{tesseractLanguage}\" ({(int)response.StatusCode}). " +
                    "It may not exist in the Tesseract language repository.");
            }

            var tempPath = path + ".download";

            await using (var fileStream = File.Create(tempPath))
            await using (var responseStream = await response.Content.ReadAsStreamAsync(token))
            {
                await responseStream.CopyToAsync(fileStream, token);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Could not download OCR language data for \"{tesseractLanguage}\". Check your internet connection, " +
                $"or download {tesseractLanguage}.traineddata manually from github.com/tesseract-ocr/tessdata_fast " +
                $"and put it in {tessdataFolder}.", ex);
        }

        Logger.Write($"OCR language data ready: {path}");
        return path;
    }
}
