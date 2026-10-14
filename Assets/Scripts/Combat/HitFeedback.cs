using UnityEngine;

/// <summary>Feel trafień: hit stop + wstrząs kamery przy trafieniu wroga, błysk ekranu + wstrząs przy obrażeniach gracza.</summary>
public class HitFeedback : MonoBehaviour
{
    [SerializeField] private Health playerHealth;

    private void OnEnable()
    {
        EnemyHealth.OnEnemyDamaged += EnemyHealth_OnEnemyDamaged;
        if (playerHealth != null)
            playerHealth.OnHealthChanged += PlayerHealth_OnHealthChanged;
    }

    private void OnDisable()
    {
        EnemyHealth.OnEnemyDamaged -= EnemyHealth_OnEnemyDamaged;
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= PlayerHealth_OnHealthChanged;
    }

    private void EnemyHealth_OnEnemyDamaged(EnemyHealth enemy, int damage)
    {
        HitStop.Freeze(0.06f);
        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(0.15f, 0.1f);
        if (enemy != null)
            HitSplash.Burst(enemy.transform.position + Vector3.up * 0.7f, HitSplash.ColorOf(enemy.gameObject), enemy.IsBoss ? 14 : 10);
        // Trafienie bossa: krótkie zbliżenie kamery (A6).
        if (enemy != null && enemy.IsBoss)
        {
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraController>() : null;
            if (cam != null) cam.PunchZoom(0.85f, 0.4f);
        }
    }

    private void PlayerHealth_OnHealthChanged(int change)
    {
        if (change >= 0)
            return;
        if (ScreenFlash.Instance != null)
            ScreenFlash.Instance.Flash();
        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(0.3f, 0.2f);
    }
}
