# Przegląd licencji — 23 listopada 2026

## Zakres i wynik

Sprawdzono pliki źródłowe i ich nagłówki, wersje i informacje producentów osadzone w wybranych DLL-ach, statyczne importy bibliotek PE, nazwy i licencje zapisane w fontach oraz materiały z oryginalnych pakietów wydawców. Oryginalne zasoby TextMeshPro porównano z pakietami `.unitypackage` obecnymi w przekazanej kopii projektu.

Zebrano potwierdzone licencje w [`Licenses`](../Licenses/) i opisano ich przypisanie w [`THIRD_PARTY_NOTICES.md`](../THIRD_PARTY_NOTICES.md). Biblioteki i zasoby mają różne licencje. Licencja MIT rdzenia GDAL nie stanowi licencji wszystkich DLL-i w `Assets/Plugins/GDAL`.

**Status publikacji całej paczki natywnej: wymaga dalszego przygotowania.** Nie stwierdzono ogólnego zakazu udostępnienia projektu, ale na podstawie przekazanej kopii nie potwierdzono wszystkich warunków redystrybucji kompletnego zestawu DLL-i i builda Windows.

## Potwierdzone ustalenia

### Kod i zasoby interfejsu

- Clipper 6.4.2 i Clipper2: Boost Software License 1.0, zgodnie z nagłówkami źródeł i [licencją projektu Clipper2](https://github.com/AngusJohnson/Clipper2/blob/main/LICENSE). Nagłówki autorstwa pozostają w źródłach.
- `Clipper2/HashCode.cs`: MIT .NET Foundation oraz informacja BSD dotycząca xxHash32; oryginalny komentarz w źródle jest zachowany.
- StandaloneFileBrowser: [MIT](https://github.com/gkngkc/UnityStandaloneFileBrowser/blob/master/LICENSE.txt). To nie zastępuje informacji o licencjach jego dołączonych bibliotek.
- Ookii.Dialogs: wersja i copyright w DLL odpowiadają wydaniu 1.0.0.0 / Sven Groot 2009. Oryginalny pakiet z [witryny autora](https://www.ookii.org/software/dialogs/) zawiera BSD z trzema warunkami. DLL w projekcie nie ma identycznego hasha jak aktualny plik z archiwum autora, więc nie potwierdzono jego pełnej historii kompilacji.
- Biblioteka `System.Windows.Forms.dll` z SFB jest zidentyfikowana w metadanych jako biblioteka klas Mono. [Informacja licencyjna Mono](https://www.mono-project.com/docs/faq/licensing/) i zestawienie licencji projektu są dołączone jako odniesienie.
- TextMeshPro 3.0.6: oryginalny `LICENSE.md` w pamięci pakietów Unity wskazuje Unity Companion License dla projektów zależnych od Unity. Zachowano jego treść i [odsyłacz do licencji Unity](https://unity.com/legal/licenses/unity-companion-license). Dotyczy to zasobów Unity, a nie nadania praw do całego kodu autora.
- Liberation Sans, Anton, Bangers i Oswald: istniejące SIL OFL 1.1 pozostają przy fontach. Roboto Bold 2.137 ma w metadanych Apache 2.0 i Copyright 2011 Google Inc.
- `Electronic Highway Sign.TTF` pochodzi z oryginalnego `TMP Examples & Extras.unitypackage`. W samym foncie i tym archiwum nie znaleziono osobnej noty licencyjnej fontu. Nie przypisano mu automatycznie SIL OFL. Przy publikowaniu samych przykładów/fontów poza projektem Unity jego warunki wymagają osobnego ustalenia.

### GDAL i pakiet natywny

- `gdal.dll` ma wersję 3.9.1. Dołączono [licencję dokładnego wydania GDAL v3.9.1](https://github.com/OSGeo/gdal/blob/v3.9.1/LICENSE.TXT). [Dokumentacja GDAL](https://gdal.org/en/stable/license.html) wyraźnie rozróżnia licencję rdzenia od licencji zależności.
- `FileGDBAPI.dll` ma wersję 1.5.1.253. SHA-256 pliku w projekcie zgadza się z `bin64/FileGDBAPI.dll` w [oficjalnym archiwum SDK Esri VS2017 1.5.1](https://github.com/Esri/file-geodatabase-api/tree/master/FileGDB_API_1.5.1). Archiwum zawiera `license/Apache License.pdf`; jego treść potwierdza Apache 2.0. Dołączono również tekst licencji z tego samego katalogu wydania. Odnalezione `userestrictions.txt` dotyczy kodu przykładowego Esri, którego projekt nie dołącza jako swoich źródeł.
- `NCSEcw.dll` ma wersję SDK 5.5.0.2034. [Dokumentacja sterownika ECW](https://gdal.org/en/stable/drivers/raster/ecw.html) opisuje różne warianty licencji, w tym bezpłatne odczytywanie na komputerach desktopowych. Sam numer wersji DLL i możliwość bezpłatnego odczytu nie rozstrzygają warunków redystrybucji konkretnego pakietu. Oryginalnej umowy do tego pliku nie było w przekazanym katalogu DLL.
- `libmysql.dll` ma wersję 8.1.0.0. [Licencja dokładnego wydania MySQL 8.1.0](https://github.com/mysql/mysql-server/blob/mysql-8.1.0/LICENSE) wskazuje GPLv2 i Universal FOSS Exception 1.0 dla klienta. [Wyjątek Oracle](https://oss.oracle.com/licenses/universal-foss-exception/) nie jest ogólnym zastąpieniem GPL licencją MIT. Należy rozstrzygnąć obowiązki dotyczące redystrybucji i źródeł dla wybranego sposobu wydania aplikacji.
- Metadane `iconv-2.dll` 1.16 określają bibliotekę jako LGPL. Dalsze zależności obejmują m.in. GEOS i SpatiaLite. Nie udokumentowano jeszcze pełnego zestawu wersji, źródeł i warunków wszystkich dołączonych binariów.
- W paczce znajdują się też pliki MapServer/MapCache, biblioteki HTTP i bazy danych. Ich obecność nie dowodzi, że wszystkie są niezbędne do odczytu GeoTIFF przez tę aplikację.

### Analiza zależności technicznych

Tabela importów `gdal.dll` bezpośrednio wskazuje `libmysql.dll`, `spatialite.dll`, `proj_9_3.dll`, `tiff.dll` i inne biblioteki. Usunięcie `libmysql.dll` bez przygotowania innej paczki runtime mogłoby uniemożliwić uruchomienie.

W przechodniej tabeli statycznych importów rdzenia i wrapperów C# nie znaleziono `NCSEcw.dll` ani `FileGDBAPI.dll`. To nie wyklucza dynamicznego ładowania przez dodatkowe sterowniki. Sama ta obserwacja nie wystarcza do stwierdzenia, że można usunąć dowolny plik bez sprawdzenia konfiguracji wydania.

## Co pozostaje do ustalenia przed dystrybucją

1. Potwierdzić redystrybucję konkretnego wariantu ECW 5.5 albo przygotować wariant runtime bez niepotrzebnych sterowników.
2. Przygotować komplet informacji i materiałów wymaganych przez faktyczne biblioteki GPL/LGPL, w tym dotyczących odpowiadających źródeł i zastosowania wyjątków. Same pliki z tekstem licencji nie zamykają tego punktu.
3. Potwierdzić źródła i uprawnienia do tła menu, ikon oraz przykładowych rastrów `TestGeoTIFF`. Nazwy plików i metadane narzędzia graficznego nie określają warunków udostępniania.
4. Wybrać sposób udostępnienia: komplet z prawidłowo przygotowanym runtime albo źródła z instrukcją pozyskania zależności od ich dostawców. Drugi wariant wymaga przygotowania odpowiedniej kopii publikacyjnej i instrukcji; obecny skrypt przywracania korzysta z archiwów dołączonych do projektu.

Powyższe warunki dotyczą także ZIP-a udostępnianego linkiem i osobnego builda Windows. Zmiana widoczności linku nie zastępuje uprawnień do rozpowszechniania plików.

## Stan projektu

Autor potwierdził 23 listopada 2026, że testy uruchomienia zakończyły się powodzeniem. W ramach tego przeglądu dodano dokumentację i informacje licencyjne. Kod aplikacji oraz biblioteki runtime pozostają w dotychczasowej postaci. Bieżący PDF pracy zastąpiono wersją publiczną z trwale usuniętym numerem albumu i prywatnymi metadanymi; oryginał pozostaje w prywatnej historii Git i zachowanych materiałach.
