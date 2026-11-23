using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pojedyncza regu³a VRT: dla wartoœci indeksu w [min,max) zastosuj intensityPercent (0..100).
/// </summary>
[System.Serializable] // przyda siê, jeœli kiedyœ zechcesz pokazaæ listê w Inspectorze
public class SprayRule
{
    // Zak³adamy indeks znormalizowany do 0..1 (np. NDVI i inne mapy -> normalizacja).
    public float minValue;          // w³¹cznie
    public float maxValue;          // wy³¹cznie (pó³otwarty przedzia³ [min,max))
    public float intensityPercent;  // 0..100
}

public enum FuelModel { LitersPerHectare, LitersPerHour }

/// <summary>
/// Zbiorczy pakiet opcji przekazywany do symulacji.
/// Tworzysz go w SimulationOptionsUIController i przekazujesz do SimulationController.StartSimulation(...).
/// </summary>
public class SimulationOptions
{
    public float velocityKmH;        // prêdkoœæ jazdy
    public float boomWidthMeters;    // szerokoœæ belki
    public int sectionCount;       // liczba sekcji
    public float productPerLWater; // iloœæ czynnika w litrze wody
    public float volumeLitersPerHa;  // bazowa objêtoœæ cieczy [L/ha]
    public List<SprayRule> sprayRules;   // tabela oprysku
    public FuelModel fuelModel = FuelModel.LitersPerHectare;
    public float fuelLitersPerHa = 12f;
    public float fuelLitersPerHour = 10f;
}

/// <summary>
/// Interfejs dostawcy intensywnoœci — na wejœciu wartoœæ indeksu (0..1), na wyjœciu %.
/// SimulationController bêdzie z niego korzysta³ podczas przejazdu.
/// </summary>
public interface ISprayIntensityProvider
{
    /// <param name="index01">Znormalizowana wartoœæ indeksu 0..1 (NDVI/itp.)</param>
    /// <returns>Intensywnoœæ w %, 0..100</returns>
    float GetIntensity(float index01);
}

/// <summary>
/// Homogeniczny oprysk: zwraca sta³y %, niezale¿nie od indeksu.
/// </summary>
public class HomogeneousProvider : ISprayIntensityProvider
{
    private readonly float percent; // 0..100

    public HomogeneousProvider(float percent)
    {
        // Ma³a asekuracja (przytnij do 0..100)
        this.percent = UnityEngine.Mathf.Clamp(percent, 0f, 100f);
    }

    public float GetIntensity(float index01) => percent;
}

/// <summary>
/// Oprysk wg regu³: szuka pasuj¹cego przedzia³u [min,max) i zwraca przypisany %.
/// Jeœli ¿aden nie pasuje — zwraca 0.
/// </summary>
public class RulesProvider : ISprayIntensityProvider
{
    private readonly List<SprayRule> rules; // posortowane po minValue

    public RulesProvider(List<SprayRule> rules)
    {
        // Kopia + sort po minValue, ¿eby wyszukiwanie by³o deterministyczne
        this.rules = (rules ?? new List<SprayRule>()).OrderBy(r => r.minValue).ToList();
    }

    public float GetIntensity(float index01)
    {
        // Bezpieczne przyciêcie
        index01 = UnityEngine.Mathf.Clamp01(index01);

        for (int i = 0; i < rules.Count; i++)
        {
            var r = rules[i];
            // pó³otwarty przedzia³: [min, max)
            if (index01 >= r.minValue && index01 < r.maxValue)
                return UnityEngine.Mathf.Clamp(r.intensityPercent, 0f, 100f);
        }
        return 0f; // brak dopasowania => nie pryskaj
    }
}
