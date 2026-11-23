# Składniki zewnętrzne

Licencje opisane poniżej dotyczą wskazanych bibliotek, fontów i innych zasobów. Wybór licencji kodu autora aplikacji i warunków udostępnienia pracy magisterskiej jest osobną decyzją.

| Składnik | Ustalenie | Materiały licencyjne |
| --- | --- | --- |
| GDAL/OGR | Natywny `gdal.dll` 3.9.1; rdzeń głównie MIT, z dodatkowymi informacjami dotyczącymi części źródeł | [`GDAL-3.9.1-LICENSE.txt`](Licenses/GDAL-3.9.1-LICENSE.txt) |
| Clipper 6.4.2 i Clipper2 | Boost Software License 1.0; Angus Johnson | [`Boost-1.0.txt`](Licenses/Boost-1.0.txt); zachowane nagłówki źródeł |
| `Clipper2/HashCode.cs` | Nagłówek .NET Foundation: MIT; zawarta implementacja xxHash32 ma informację BSD 2-Clause | [`DotNet-MIT.txt`](Licenses/DotNet-MIT.txt); oryginalny komentarz BSD pozostaje w pliku |
| Unity Standalone File Browser | MIT; Copyright 2017 Gökhan Gökçe | [`StandaloneFileBrowser-MIT.txt`](Licenses/StandaloneFileBrowser-MIT.txt) |
| Ookii.Dialogs | Zasób z numerem wersji 1.0.0.0 i copyrightem Sven Groot 2009; BSD z trzema warunkami | [`Ookii-Dialogs-1.0-BSD.txt`](Licenses/Ookii-Dialogs-1.0-BSD.txt) |
| `System.Windows.Forms.dll` w SFB | Metadane identyfikują bibliotekę klas Mono; główna licencja MIT, z informacjami o składnikach zewnętrznych | [`Mono-LICENSE.txt`](Licenses/Mono-LICENSE.txt) |
| TextMeshPro 3.0.6 | Unity Companion License dla projektów zależnych od Unity | Oryginalna nota pakietu: [`TextMeshPro-3.0.6-LICENSE.md`](Licenses/TextMeshPro-3.0.6-LICENSE.md) |
| Liberation Sans, Anton, Bangers, Oswald | SIL OFL 1.1 | Oryginalne pliki `*OFL.txt` pozostają obok fontów w `Assets/TextMesh Pro` |
| Roboto Bold 2.137 | Metadane fontu: Apache License 2.0; Copyright 2011 Google Inc. | [`Apache-2.0.txt`](Licenses/Apache-2.0.txt) |
| Esri FileGDB API 1.5.1.253 | Dołączony DLL jest identyczny z `bin64/FileGDBAPI.dll` oficjalnego SDK VS2017 1.5.1; jego archiwum zawiera Apache 2.0 | [`FileGDBAPI-1.5.1-Apache-2.0.txt`](Licenses/FileGDBAPI-1.5.1-Apache-2.0.txt) |
| MySQL Client Library 8.1 | GPLv2 z Universal FOSS Exception 1.0; komponent nie jest objęty MIT GDAL-a | Pełna informacja wydawcy: [`MySQL-8.1-LICENSE.txt`](Licenses/MySQL-8.1-LICENSE.txt) |
| libiconv / pozostałe biblioteki natywne | W paczce występują także biblioteki na LGPL i innych licencjach | [`LGPL-2.1.txt`](Licenses/LGPL-2.1.txt); szczegółowy stan w [audycie](docs/LICENSE_AUDIT.md) |

Samo zebranie powyższych tekstów nie oznacza jeszcze spełnienia wszystkich warunków rozpowszechniania całego builda lub paczki bibliotek natywnych. W szczególności wymogi dotyczące źródeł, wyjątków GPL/LGPL oraz wariantu licencji ECW wymagają osobnego domknięcia. Nie należy oznaczać całej paczki DLL jako MIT.

[Zakres sprawdzenia i pozostałe ustalenia](docs/LICENSE_AUDIT.md).


## Przygotowanie kopii publicznej

Z biblioteki StandaloneFileBrowser dla macOS usunięto ścieżki źródeł w symbolach debugowania, zachowując wszystkie sekcje, tablicę symboli i polecenia ładowania. Z `mod_mapcache.dll` usunięto dane debugowania/PDB oraz ścieżkę źródła używaną w diagnostyce; sekcje wykonywalne pozostały niezmienione. W Clipper2 pozostawiono wyłącznie klucz publiczny i włączono podpisywanie odroczone w jego osobnym projekcie. Unity kompiluje źródła Clipper2 bez korzystania z tego ustawienia. Zachowano autorstwo i licencje składników. Z grafik JPEG/PNG usunięto zbędne metadane edytora bez zmiany pikseli.
