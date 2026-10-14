using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Ruch po dowolnej powierzchni bez NavMesha (klif, ściana, strop). Sterowany przez SpiderController:
/// Begin(cel) znajduje powierzchnię obok pająka, potem co klatkę trzyma przyczepienie raycastem w -up,
/// orientuje transform.up do normalnej powierzchni (slerp alignSpeed/s) i porusza się ku celowi
/// rzutowanemu na płaszczyznę powierzchni z prędkością climbSpeed * SpeedMultiplier. Bez trafienia owija krawędź
/// (raycast skośnie do tyłu zza krawędzi), przed przeszkodą (wklęsły róg) przechodzi na jej normalną.
/// Wspinaczka (stopWhenOnFloor): cel jest podniesiony o climbOvershoot, a gdy pająk dojdzie do wysokości półki,
/// robi krótki skok-wejście (vault) na punkt NavMesha i zgłasza OnReachedFloor.
/// Raycasty przez SpiderSpawnHelper.Raycast (tylne ściany, normalne zwrócone do pająka). Wymaga Rigidbody kinematycznego.
/// </summary>
public class WallCrawler : MonoBehaviour
{
    public event System.Action OnReachedFloor;

    [SerializeField] private float climbSpeed = 4.5f;
    [SerializeField, Tooltip("Zasięg raycastu przyczepienia w -up (m).")] private float attachRange = 1.2f;
    [SerializeField] private float alignSpeed = 8f;
    [SerializeField, Tooltip("Odległość pivotu od powierzchni.")] private float surfaceOffset = 0.1f;
    [SerializeField] private float snapSpeed = 15f;
    [SerializeField, Tooltip("Cel wspinaczki leży o tyle wyżej niż półka, żeby pająk nie stanął tuż pod krawędzią.")] private float climbOvershoot = 1.5f;
    [SerializeField] private float vaultDuration = 0.22f;
    [SerializeField] private float vaultArc = 0.35f;

    public bool Active { get; private set; }
    public Vector3 FloorPoint { get; private set; }
    /// <summary>Sekundy bez żadnej powierzchni pod/za pająkiem (kontroler może uznać go za zgubionego).</summary>
    public float LostTime { get; private set; }
    /// <summary>Normalna powierzchni, do której pająk jest teraz przyklejony (transform.up zbliża się do niej).</summary>
    public Vector3 SurfaceNormal { get; private set; } = Vector3.up;
    public bool IsVaulting => vaulting;
    public float SpeedMultiplier = 1f;

    private Vector3 target;      // dokąd pająk zmierza (rzutowane na powierzchnię)
    private Vector3 floorTarget; // punkt półki (tylko wspinaczka)
    private bool stopAtFloor;
    private bool vaulting;
    private float vaultT;
    private Vector3 vaultStart, vaultEnd;
    private Quaternion vaultRot;

    private bool Cast(Vector3 origin, Vector3 dir, float distance, out RaycastHit best)
        => SpiderSpawnHelper.Raycast(origin, dir, distance, transform, out best);

    /// <summary>Szuka powierzchni w 1,5 m wokół pająka (7 osi, potem 8 poziomych do 2,5 m) i przykleja się do najbliższej. False, gdy nic nie ma.</summary>
    public bool Begin(Vector3 targetPoint, bool stopWhenOnFloor)
    {
        floorTarget = targetPoint;
        target = stopWhenOnFloor ? targetPoint + Vector3.up * climbOvershoot : targetPoint;
        stopAtFloor = stopWhenOnFloor;
        vaulting = false;
        LostTime = 0f;
        Vector3 pos = transform.position + transform.up * 0.3f;
        bool found = false;
        RaycastHit bestHit = default;
        float bestDist = float.MaxValue;
        foreach (var dir in new[] { -transform.up, transform.forward, -transform.forward, transform.right, -transform.right, Vector3.down, Vector3.up })
        {
            if (Cast(pos, dir, attachRange + 0.3f, out var hit) && hit.distance < bestDist)
            {
                bestHit = hit;
                bestDist = hit.distance;
                found = true;
            }
        }
        if (!found)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                if (Cast(pos, dir, 2.5f, out var hit) && hit.distance < bestDist)
                {
                    bestHit = hit;
                    bestDist = hit.distance;
                    found = true;
                }
            }
        }
        Active = found;
        if (found)
        {
            SurfaceNormal = bestHit.normal;
            Vector3 fwd = Vector3.ProjectOnPlane(target - transform.position, bestHit.normal);
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = Vector3.ProjectOnPlane(transform.forward, bestHit.normal);
            transform.rotation = SpiderSpawnHelper.SurfaceRotation(bestHit.normal, fwd);
            transform.position = bestHit.point + bestHit.normal * surfaceOffset;
        }
        return found;
    }

    public void SetTarget(Vector3 targetPoint) => target = targetPoint;

    public void Stop()
    {
        Active = false;
        vaulting = false;
    }

    private void BeginVault()
    {
        vaulting = true;
        vaultT = 0f;
        vaultStart = transform.position;
        vaultRot = transform.rotation;
        vaultEnd = NavMesh.SamplePosition(floorTarget, out var nav, 2f, NavMesh.AllAreas) ? nav.position : floorTarget;
        FloorPoint = vaultEnd;
    }

    private void UpdateVault(float dt)
    {
        vaultT += dt / Mathf.Max(0.05f, vaultDuration);
        float t = Mathf.Clamp01(vaultT);
        float e = t * t * (3f - 2f * t);
        transform.position = Vector3.Lerp(vaultStart, vaultEnd, e) + Vector3.up * (vaultArc * 4f * t * (1f - t));
        Vector3 flat = vaultEnd - vaultStart;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f)
            flat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (flat.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(vaultRot, Quaternion.LookRotation(flat.normalized, Vector3.up), e);
        SurfaceNormal = Vector3.up;
        if (t < 1f)
            return;
        transform.position = vaultEnd;
        vaulting = false;
        Active = false;
        OnReachedFloor?.Invoke();
    }

    private void Update()
    {
        if (!Active)
            return;
        float dt = Time.deltaTime;
        if (vaulting)
        {
            UpdateVault(dt);
            return;
        }

        Vector3 up = transform.up;
        Vector3 pos = transform.position;

        // wspinaczka: pivot doszedł do wysokości półki i półka jest tuż obok = wejście na nią
        if (stopAtFloor && pos.y >= floorTarget.y - 0.1f)
        {
            Vector3 d = pos - floorTarget;
            d.y = 0f;
            if (d.magnitude < 4f)
            {
                BeginVault();
                return;
            }
        }

        Vector3 toTarget = Vector3.ProjectOnPlane(target - pos, up);
        float reach = toTarget.magnitude;
        Vector3 dir = reach > 0.05f ? toTarget / reach : Vector3.ProjectOnPlane(transform.forward, up).normalized;
        float step = reach > 0.05f ? Mathf.Min(climbSpeed * SpeedMultiplier * dt, reach) : 0f;

        Vector3 newUp = up;
        bool haveSurface = false;
        Vector3 surfacePoint = default, surfaceNormal = up;

        // wklęsły róg: przeszkoda tuż przed nosem
        if (step > 0f && Cast(pos + up * 0.25f, dir, 0.5f + step, out var ahead) && Vector3.Dot(ahead.normal, up) < 0.9f)
        {
            newUp = ahead.normal;
            step = Mathf.Min(step, Mathf.Max(ahead.distance - 0.3f, step * 0.25f));
            haveSurface = true;
            surfacePoint = ahead.point;
            surfaceNormal = ahead.normal;
        }

        pos += dir * step;

        // przyczepienie
        if (Cast(pos + up * 0.3f, -up, 0.3f + attachRange, out var below))
        {
            if (!haveSurface)
                newUp = below.normal;
            haveSurface = true;
            surfacePoint = below.point;
            surfaceNormal = below.normal;
            pos = Vector3.Lerp(pos, below.point + below.normal * surfaceOffset, 1f - Mathf.Exp(-snapSpeed * dt));
            LostTime = 0f;
        }
        else if (Cast(pos + up * 0.3f, (-up - dir).normalized, 1.5f, out var wrap))
        {
            // wypukła krawędź: pająk wyszedł poza nią, powierzchnia jest skośnie za nim (w głąb i wstecz)
            newUp = wrap.normal;
            haveSurface = true;
            surfacePoint = wrap.point;
            surfaceNormal = wrap.normal;
            pos = Vector3.Lerp(pos, wrap.point + wrap.normal * surfaceOffset, 1f - Mathf.Exp(-snapSpeed * dt));
            LostTime = 0f;
        }
        else
            LostTime += dt;

        if (haveSurface)
            SurfaceNormal = newUp;

        Vector3 look = Vector3.ProjectOnPlane(dir, newUp);
        if (look.sqrMagnitude < 0.0001f)
            look = Vector3.ProjectOnPlane(transform.forward, newUp);
        if (look.sqrMagnitude < 0.0001f)
            look = Vector3.ProjectOnPlane(Vector3.forward, newUp);
        transform.position = pos;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look.normalized, newUp), 1f - Mathf.Exp(-alignSpeed * dt));

        if (stopAtFloor && haveSurface && surfaceNormal.y > 0.85f
            && NavMesh.SamplePosition(surfacePoint, out var nav, 1f, NavMesh.AllAreas) && Mathf.Abs(nav.position.y - surfacePoint.y) < 0.6f)
        {
            FloorPoint = nav.position;
            Active = false;
            OnReachedFloor?.Invoke();
        }
    }
}
