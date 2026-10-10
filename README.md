# Screen Translator

A Windows desktop app that captures the screen, recognizes text with OCR, translates it, and draws the translation on top of the original text.

## Requirements

- Windows 10 build 19041 or newer
- .NET 10 SDK
- Visual Studio 2026 (or any IDE with .NET 10 support)

## Build

```
dotnet restore
dotnet build -c Release
dotnet run
```

## Publish

```
dotnet publish -c Release
```

The result is a self-contained build under
`bin\Release\net10.0-windows...\win-x64\publish\` — no separate .NET runtime
install is needed on the target machine, just the `publish` folder as-is.

Note: this is **not** a single .exe. Tesseract's native DLLs
(leptonica/tesseract) fail to load reliably when bundled inside a
`PublishSingleFile` exe - the extraction step that single-file publish relies
on doesn't play well with this particular native library. If you want a
single file to hand someone, zip the `publish` folder instead.

## Project layout

```
Models/                  settings and OCR data types
Native/                  Win32 interop
Localization/Loc.cs      English and Persian UI strings
Services/
  SettingsService.cs     JSON settings in %AppData%\ScreenTranslator
  LanguageCatalog.cs     supported translation languages
  TranslationPipeline.cs capture to overlay orchestration
  ColorSampler.cs        background and foreground color per text block
  StartupRegistration.cs run at Windows startup
  Capture/               screen capture
  Ocr/                   Windows.Media.Ocr and Tesseract engines
  Text/                  line grouping and script based language detection
  Translation/           Azure, DeepL, LibreTranslate, offline ONNX
  Input/                 low level keyboard hook and hotkey manager
Views/
  FloatingIconWindow     edge icon with roll out panel
  OverlayWindow          click through topmost overlay
  RegionSelectWindow     drag to choose a capture region
  SettingsWindow         tabbed settings
```

## How the flow works

1. The global keyboard hook sees the configured key.
2. `GdiScreenCapture` grabs the active monitor or the selected region.
3. `WindowsOcrEngine` returns lines with bounding boxes.
4. `BlockGrouper` merges lines into paragraphs.
5. `LanguageDetector` picks the source language when auto detect is on.
6. The selected `ITranslator` translates all blocks in batches.
7. `ColorSampler` picks a background and text color per block.
8. `OverlayWindow` draws each translation at the original position.

## Configuring a translation provider

No API keys ship with the app. Open Settings and enter your own:

- Azure Translator: key and region from the Azure portal free tier
- DeepL: free API key, keys ending in `:fx` use the free endpoint
- LibreTranslate: your own server URL, or any instance you are allowed to use
- Cloud AI: your own key for Google Gemini, OpenAI, Anthropic (Claude), OpenRouter, Groq, DeepSeek, Mistral, or any OpenAI-compatible address. All recognized blocks go out in one request, and the answer is parsed as a JSON array
- Offline: a folder containing one subfolder per language pair, named `en-fa`, `zh-ja`, etc.

API keys are stored in `%AppData%\ScreenTranslator\settings.json` encrypted with Windows DPAPI (current user scope). A settings file copied to another PC or user account cannot be decrypted, so the keys must be entered again there.

## Remaining work

- `OfflineTranslator.TranslateAsync` needs the ONNX encoder/decoder and SentencePiece tokenizer
- Mixed DPI multi monitor setups need one overlay window per monitor
- `Windows.Graphics.Capture` is a better capture backend than GDI for HDR and hardware accelerated surfaces

## Licensing

See `THIRD-PARTY-NOTICES.txt`. Everything used here is MIT, Apache 2.0 or CC BY 4.0. No undocumented endpoints of commercial translation services are called.
