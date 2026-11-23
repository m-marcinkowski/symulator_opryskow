# Symulator oprysków w rolnictwie precyzyjnym

Projekt aplikacji desktopowej opracowanej w ramach pracy magisterskiej. Symulator pozwala zaplanować przejazdy opryskiwacza na wyznaczonym polu i porównać oprysk jednorodny z dawkowaniem zależnym od wskaźnika roślinności.

**Autor:** Michał Marcinkowski  
**Technologie:** Unity 2022.3.31f1, C#, GDAL, Unity UI / TextMeshPro.

## Praca magisterska

[Praca magisterska — publiczna wersja PDF](docs/Praca_magisterska.pdf)

Z dokumentu trwale usunięto numer albumu i prywatne metadane. Autorstwo, dane promotora, treść techniczna, bibliografia oraz wszystkie 49 stron zostały zachowane. Oryginał pozostaje w prywatnej historii Git i zachowanych materiałach.

## Możliwości

- Wczytywanie pary georeferencjonowanych obrazów GeoTIFF: RGB i CIR.
- Wizualizacja obrazu i wskaźników roślinności, m.in. NDVI.
- Wyznaczanie granic pola oraz planowanie równoległych przejazdów z zakrętami.
- Konfiguracja prędkości, szerokości belki, liczby sekcji, dawki cieczy i zużycia paliwa.
- Oprysk jednorodny lub zmienna dawka na podstawie zdefiniowanych przedziałów wartości wskaźnika.
- Symulacja przejazdu ze sterowaniem sekcjami opryskiwacza.
- Podsumowanie czasu, dystansu, pokrycia pola oraz zużycia wody, preparatu i paliwa.
- Zapis i odczyt przygotowanego pola oraz ścieżek w pliku `project.sprproj` (JSON) z towarzyszącymi obrazami PNG.

## Uruchomienie w Unity

Projekt jest przygotowany do pracy w **Windows x64**. W repozytorium znajdują się dołączone do projektu biblioteki natywne GDAL; dostępność zależności dla innych systemów nie została zweryfikowana. Trzy największe biblioteki są zapisane bezstratnie w `NativeDependencies` jako archiwa GZip.

1. Zainstaluj Unity Hub oraz edytor **Unity 2022.3.31f1**.
2. Pobierz lub sklonuj repozytorium.
3. W Unity Hub wybierz **Add project from disk** i wskaż główny katalog zawierający `Assets`, `Packages` i `ProjectSettings`.
4. Otwórz projekt i poczekaj na import zasobów oraz odtworzenie pakietów. Przy pierwszym uruchomieniu potrzebne jest połączenie z internetem. Skrypt edytora automatycznie rozpakowuje trzy biblioteki GDAL i weryfikuje ich sumy SHA-256. W razie potrzeby można ponowić tę czynność przez **Tools → Spraying Simulator → Restore GDAL libraries**.
5. Otwórz scenę `Assets/Scenes/MainMenuScene.unity` i naciśnij **Play**.

Do odtworzenia projektu nie jest potrzebny katalog `Library`; Unity wygeneruje go lokalnie. Zawarte w `Assets` pliki `.meta` i biblioteki należy zachować.

## Przykładowa symulacja

1. W menu głównym rozpocznij nowy projekt.
2. Wybierz `orto_2022_RGB.tif` i `orto_2022_CIR.tif` **z tego samego podkatalogu** `TestGeoTIFF/#1` albo `TestGeoTIFF/#2`.
3. Wczytaj dane, wybierz wizualizację i zaznacz granicę pola.
4. Przygotuj i zatwierdź ścieżkę przejazdu.
5. Ustaw parametry opryskiwacza oraz sposób dawkowania, a następnie uruchom symulację.
6. Po zakończeniu odczytaj zestawienie wyników.

Przy wczytywaniu własnych danych należy użyć odpowiadających sobie obrazów RGB i CIR o zgodnej georeferencji. Wyniki opisują model symulacyjny zastosowany w projekcie.

Zapisy projektu trafiają do podkatalogu `Saves` w systemowym katalogu Dokumenty użytkownika. Do przeniesienia zapisu potrzebny jest cały jego folder, łącznie z obrazami PNG.

## Struktura repozytorium

| Katalog | Zawartość |
| --- | --- |
| `Assets/Scripts` | Logika wczytywania danych, wyznaczania pola, planowania przejazdów i symulacji |
| `Assets/Scenes` | Sceny aplikacji |
| `Assets/Plugins` | Dołączone biblioteki, w tym GDAL i Clipper2 |
| `Assets/StandaloneFileBrowser` | Wybór plików w interfejsie systemowym |
| `Assets/Editor` | Automatyczne odtworzenie spakowanych bibliotek GDAL |
| `NativeDependencies` | Trzy duże biblioteki GDAL w formacie GZip oraz manifest z sumami SHA-256 |
| `Packages` | Manifest i wersje pakietów Unity |
| `ProjectSettings` | Konfiguracja i wersja edytora Unity |
| `TestGeoTIFF` | Dwa przykładowe zestawy danych RGB/CIR |
| `docs` | Publiczna wersja pracy magisterskiej w PDF i dokumentacja |

Repozytorium pomija pamięć podręczną edytora, logi, ustawienia użytkownika i wygenerowane pliki IDE. Starsze warianty skryptów i scen zachowano zgodnie z przekazanym projektem.

## Build Windows

Aby utworzyć aplikację, wybierz w Unity **File → Build Settings**, ustaw **PC, Mac & Linux Standalone → Windows → x86_64** i użyj listy scen zapisanej w projekcie. Sceną startową jest `MainMenuScene`.

Gotową aplikację należy przenosić jako **cały katalog builda**, z plikiem `.exe`, katalogiem `_Data`, bibliotekami DLL i pozostałymi plikami wygenerowanymi przez Unity. Sam plik `.exe` nie wystarcza. Wygenerowany build jest dystrybuowany osobno od kodu źródłowego.

## Zależności

Projekt korzysta z komponentów zewnętrznych, m.in. Unity, GDAL, Clipper/Clipper2, StandaloneFileBrowser i TextMeshPro. Prawa i warunki licencyjne tych komponentów należą do ich autorów. Dołączenie kodu do tego repozytorium nie nadaje im nowej licencji.

## Licencje zależności

[Składniki zewnętrzne i ich licencje](THIRD_PARTY_NOTICES.md) oraz [zakres przeglądu i pozostałe ustalenia](docs/LICENSE_AUDIT.md).
