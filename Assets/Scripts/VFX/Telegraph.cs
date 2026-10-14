using UnityEngine;

/// <summary>
/// Telegrafy ataków na ziemi (KONTRAKT). AI woła te metody w momencie decyzji o ataku; grafika je implementuje
/// (płaskie quady z teksturą rysowaną w kodzie, bez decali URP). Dopóki nie ma implementacji, nic nie rysują.
/// Czas `seconds` = długość windupu; kształt ma wypełniać się do końca windupu, potem znika.
/// </summary>
public static class Telegraph
{
    /// <summary>Implementacja podpinana przez grafikę (TelegraphRenderer w scenie). Null = brak rysowania.</summary>
    public static ITelegraphRenderer Renderer { get; set; }

    /// <summary>Okrąg na ziemi: obszar ciosu slime'a, lądowania bossa, uderzenia w ziemię.</summary>
    public static void Circle(Vector3 center, float radius, float seconds) => Renderer?.Circle(center, radius, seconds);

    /// <summary>Stożek od punktu w kierunku: skok pająka, szarża bossa.</summary>
    public static void Cone(Vector3 origin, Vector3 direction, float angleDeg, float range, float seconds) => Renderer?.Cone(origin, direction, angleDeg, range, seconds);

    /// <summary>Linia od A do B: celowanie strzelca.</summary>
    public static void Line(Vector3 from, Vector3 to, float seconds) => Renderer?.Line(from, to, seconds);
}

public interface ITelegraphRenderer
{
    void Circle(Vector3 center, float radius, float seconds);
    void Cone(Vector3 origin, Vector3 direction, float angleDeg, float range, float seconds);
    void Line(Vector3 from, Vector3 to, float seconds);
}
