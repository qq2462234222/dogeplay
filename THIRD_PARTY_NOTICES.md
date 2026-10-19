# Third-party notices

## OpenScore Lieder

Repository: https://github.com/OpenScore/Lieder

License: CC0-1.0. The upstream license is included as `assets/openscore/LICENSE.txt`.

Bundled score: Jane Bingham Abbott, **Just for Today**.

Source: https://github.com/OpenScore/Lieder/blob/main/scores/Abbott%2C_Jane_Bingham/_/Just_for_Today/lc6583477.mxl

`assets/openscore/catalog.json` contains a directory snapshot of 1,462 upstream MXL scores. Titles and composer names are derived from the repository paths, with Git blob hashes retained for download integrity checking. The app can refresh this directory from the official GitHub tree API and download selected score files from the official raw-content host.

Scores are parsed locally. No upstream program code is copied.

## SMAP community score directory

Repository: https://github.com/lingyunalingyun/Sky-MusaAutoPlay-SMAP-

Directory: https://github.com/lingyunalingyun/Sky-MusaAutoPlay-SMAP-/tree/main/曲谱库

`assets/smap/catalog.json` includes 1,601 TXT/JSON directory entries (including the repository's `Installer/sample-songs` examples) from tree `0416e82fa95c472de2de7eaf96ddd05445db73fc`, retrieved 2026-10-01. No song content or program source is bundled from this repository. Selected songs are downloaded from its official raw-content URL at the user's request and parsed by our own SkyStudio reader.

The project code is GPL-3.0. Its README explicitly says “曲谱版权归原创谱者所有”. This is not treated as a GPL license or public-domain dedication for the underlying songs. Source and author metadata are retained for downloaded scores, with a link back to the original file.

## SkyAutoPlayerScript community score directory

Repository: https://github.com/StageGuard/SkyAutoPlayerScript

Directory: https://github.com/StageGuard/SkyAutoPlayerScript/tree/master/shared_sheets

`assets/sky/catalog.json` includes 178 TXT directory entries from tree `38c688937284c9f70e72fb7f0a55d7564a6f8a54`, retrieved 2026-10-01. Author labels, when present, come from the upstream `shared_sheets.json`. No program code or song content is bundled. Selected scores are downloaded on demand, hash-checked and locally parsed.

The upstream program is LGPL-2.1; that program license is not represented as clearance of underlying musical works or community contributions. The app labels this as a community source and links to each original score.

## Josh's anime sheet music collection

Public index: https://josh.agarrado.net/music/anime/

`assets/josh/catalog.json` contains 72 PDF links listed by the public index, including entries whose page identifies Animenz / A叔 as transcriber. The PDF files are not bundled. The app downloads a selected public PDF on demand and sends it through the existing local Audiveris importer. The page and individual files retain their original authors' rights and terms; the source link is shown in the catalog.

## Audiveris 5.11.0

Repository and corresponding source: https://github.com/Audiveris/audiveris/tree/5.11.0

Release: https://github.com/Audiveris/audiveris/releases/tag/5.11.0

Downloaded Windows console distribution:
https://github.com/Audiveris/audiveris/releases/download/5.11.0/Audiveris-5.11.0-windowsConsole-x86_64.msi

License: GNU Affero General Public License version 3. The upstream license is included as `assets/AUDIVERIS-LICENSE.txt`.

The official Windows runtime is bundled in the binary release under `tools/audiveris/Audiveris`. This release does not include application source. The corresponding Audiveris source is available from the upstream repository above; Audiveris is a separate, unmodified program invoked through its batch interface. The app invokes its documented batch / transcribe / export interface. Local developer installations under tools/audiveris are excluded from the source tree.

Development fixtures under test-output are excluded from publication.

## PiaStudy / 天天钢琴

Website: https://piastudy.com/

Search and score-detail metadata are read from public Astro component props. Public `.ibe` score data is decoded using the transformation performed by the site's own public player (XOR followed by Brotli) and parsed by the existing MusicXML importer. No site JavaScript is bundled or evaluated by the HTTP importer. Public lookup does not send authentication tokens. Website login, browser verification and authenticated downloads take place in an isolated WebView2 profile using the site's own UI. Restricted scores are directed to the website's download flow; a local flag or token string is not treated as proof of permission.

No PiaStudy score contents are bundled. Public availability is not represented as an open-source license for the music. Imported songs retain the source detail page and the site's rights notice. The adapter was developed against the pages available in October 2026; future site changes may require updates.

## BrotliSharpLib 0.3.3

Source: https://github.com/master131/BrotliSharpLib

Package: https://api.nuget.org/v3-flatcontainer/brotlisharplib/0.3.3/brotlisharplib.0.3.3.nupkg

The unmodified .NET Framework 4.0 DLL is bundled in `assets/vendor/BrotliSharpLib.dll` and copied beside the executable. This target has no additional NuGet runtime dependencies. License: MIT; full text in `assets/vendor/BrotliSharpLib-LICENSE.txt`. Used for Brotli score decompression.

## Microsoft.Web.WebView2 SDK 1.0.2903.40

Package: https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/1.0.2903.40/microsoft.web.webview2.1.0.2903.40.nupkg

The unmodified net462 Core and WPF assemblies and win-x64 WebView2Loader.dll are bundled in `assets/vendor` and copied beside the executable. The SDK redistribution license and notices are included as `WebView2-LICENSE.txt` and `WebView2-NOTICE.txt`. The separately installed Microsoft Edge WebView2 Runtime is used and is not bundled. The application does not alter the site's login UI or export browser credentials.

## Windows components

Windows Media OCR, WPF, .NET Framework, WinMM MIDI and SendInput are provided by Windows. They are used through system APIs and are not redistributed here.

## Investigated but not incorporated

https://github.com/sky-music/sky-python-music-sheet-maker (MIT) generates Sky visual music sheets. No code or assets from that repository are included.
