# Dołączone biblioteki GDAL

`FileGDBAPI.dll.gz`, `gdal.dll.gz` i `spatialite.dll.gz` zawierają oryginalne biblioteki z przekazanego projektu, spakowane bezstratnie algorytmem GZip. Pliki zostały skompresowane ze względu na limit pojedynczego przesyłania do repozytorium.

Skrypt `Assets/Editor/RestoreGdalLibraries.cs` przy otwarciu projektu automatycznie odtwarza je w `Assets/Plugins/GDAL` i sprawdza rozmiary oraz sumy SHA-256 zapisane w `manifest.json`. Nie pobiera niczego z sieci i nie zastępuje istniejących bibliotek. Pliki `.meta` pozostają w oryginalnych lokalizacjach.

Archiwa można również rozpakować narzędziem obsługującym GZip i umieścić DLL-e w ścieżkach wskazanych w manifeście. Odtworzone biblioteki są wyłączone z Git przez `.gitignore`; ich źródłem pozostają archiwa w tym katalogu.
