# ScreenSense AI

A Windows desktop app that captures your screen, recognizes the text with OCR, translates it, and draws the translation on top of the original text.

## Download

Get the latest installer from the [Releases](../../releases) page and run `ScreenTranslator-Setup`. The installer is not code signed, so Windows SmartScreen may show a warning the first time; choose "More info" and then "Run anyway".

## Features

- Floating icon at the screen edge with a roll-out panel
- Translate the full screen, a saved region, or a region you pick each time, each with its own hotkey
- Hotkeys can be keyboard keys, mouse buttons or the mouse wheel, and are changeable in Settings
- Translation is drawn in place over the original text, with colors sampled from the original
- Text viewer (extracted and translated text) and a manual text translation window
- OCR for about 100 languages, language packs download automatically
- English and Persian interface
- Works out of the box with an offline dictionary; better providers can be switched on in Settings

## Translation providers

No API keys ship with the app. Open Settings, choose a provider and enter your own details:

| Provider | Needs | Notes |
|---|---|---|
| Offline dictionary | nothing | Default. Word by word, English and Persian only |
| LibreTranslate | a server URL | Can run on your own machine |
| Ollama | Ollama and a local model | Offline, editable prompt and temperature |
| LM Studio | LM Studio with its local server started | Offline, editable prompt and temperature |
| Cloud AI | your own API key | Google Gemini, OpenAI, Anthropic (Claude), OpenRouter, Groq, DeepSeek, Mistral, or any OpenAI-compatible address |
| Azure Translator | key and region | Free tier available |
| DeepL | API key | Keys ending in `:fx` use the free endpoint |

The Cloud AI provider sends all recognized text blocks in a single request and expects a JSON array back. If the reply cannot be parsed it falls back to translating one block at a time.

## Privacy and security

- With Cloud AI, Azure and DeepL, the text recognized on your screen is sent to that service. Free plans of some services may use submitted text to improve their models, so do not translate private content with them. Ollama, LM Studio, LibreTranslate on your own machine and the offline dictionary keep everything on your computer.
- API keys are stored in `%AppData%\ScreenTranslator\settings.json`, encrypted with Windows DPAPI for the current user. A settings file copied to another PC or user account cannot be decrypted, so keys must be entered again there.
- Screenshots are processed in memory and are not saved.

## Requirements

- Windows 10 build 19041 or newer, 64-bit
- To build: .NET 10 SDK and Visual Studio 2026 (or any IDE with .NET 10 support)

## Build

```
dotnet restore
dotnet build -c Release
dotnet run
```

## Publish

```
dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

The result in `publish` is self-contained, so no separate .NET runtime install is needed on the target machine.

This is **not** a single .exe. Tesseract's native DLLs fail to load reliably when bundled inside a `PublishSingleFile` exe, so keep the `publish` folder as it is.

## Build the installer

Install [Inno Setup](https://jrsoftware.org/isinfo.php), publish as above, then compile `ScreenTranslator.iss`:

```
"C:\Program Files\Inno Setup 7\ISCC.exe" /DAppVersion=1.0.0 ScreenTranslator.iss
```

The installer is written to `dist\`.

## Project layout

```
Models/                  settings and OCR data types
Native/                  Win32 interop
Localization/Loc.cs      English and Persian UI strings
Services/
  SettingsService.cs     JSON settings in %AppData%\ScreenTranslator, keys encrypted with DPAPI
  LanguageCatalog.cs     supported translation languages
  TranslationPipeline.cs capture to overlay orchestration
  ColorSampler.cs        background and foreground color per text block
  StartupRegistration.cs run at Windows startup
  Capture/               screen capture
  Ocr/                   Windows.Media.Ocr and Tesseract engines
  Text/                  line grouping and script based language detection
  Translation/           dictionary, LibreTranslate, Ollama, LM Studio, Cloud AI, Azure, DeepL, offline ONNX
  Input/                 low level keyboard and mouse hooks, hotkey manager
Views/
  FloatingIconWindow     edge icon with roll out panel
  OverlayWindow          click through topmost overlay
  RegionSelectWindow     drag to choose a capture region
  SettingsWindow         tabbed settings
  TextViewWindow         extracted and translated text
  ManualTranslateWindow  type text and translate it
```

## How the flow works

1. The global hook sees the configured hotkey.
2. `GdiScreenCapture` grabs the active monitor or the selected region.
3. The OCR engine (Tesseract by default, or Windows built-in) returns lines with bounding boxes.
4. `BlockGrouper` merges lines into paragraphs.
5. `LanguageDetector` picks the source language when auto detect is on.
6. The selected `ITranslator` translates all blocks in batches.
7. `ColorSampler` picks a background and text color per block.
8. `OverlayWindow` draws each translation at the original position.

## Remaining work

- `OfflineTranslator.TranslateAsync` needs the ONNX encoder/decoder and SentencePiece tokenizer
- Mixed DPI multi monitor setups need one overlay window per monitor
- `Windows.Graphics.Capture` is a better capture backend than GDI for HDR and hardware accelerated surfaces

## Licensing

This project is licensed under the terms in `LICENSE`. Third-party components are listed in `THIRD-PARTY-NOTICES.txt`; everything used here is MIT, Apache 2.0 or CC BY 4.0. No undocumented endpoints of commercial translation services are called.
