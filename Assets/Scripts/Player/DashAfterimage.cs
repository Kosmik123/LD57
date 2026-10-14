using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Powidoki postaci podczas dasha (BakeMesh co 0,04 s, fade 0,3 s, maks. 8 naraz).</summary>
public class DashAfterimage : MonoBehaviour
{
    private const int MaxAlive = 8;
    private const float Interval = 0.04f;
    private const float FadeTime = 0.3f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] private PlayerController player;
    [SerializeField] private SkinnedMeshRenderer skinnedRenderer;
    [SerializeField] private Material ghostMaterial;
    [SerializeField] private Color ghostColor = new Color(0.4f, 0.7f, 1f, 0.5f);

    private class Ghost
    {
        public GameObject Go;
        public Mesh Mesh;
        public MeshRenderer Renderer;
        public MaterialPropertyBlock Block = new MaterialPropertyBlock();
        public float Age;
    }

    private readonly List<Ghost> alive = new List<Ghost>();
    private Coroutine routine;

    private void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (skinnedRenderer == null) skinnedRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        if (ghostMaterial == null) ghostMaterial = SwordTrail.CreateTransparentMaterial("Afterimage (runtime)", Color.white);
    }

    private void OnEnable()
    {
        if (player != null) player.OnDashed += Player_OnDashed;
    }

    private void OnDisable()
    {
        if (player != null) player.OnDashed -= Player_OnDashed;
        routine = null;
    }

    private void OnDestroy()
    {
        foreach (var g in alive) Release(g);
        alive.Clear();
    }

    private void Player_OnDashed()
    {
        if (skinnedRenderer == null) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Emit());
    }

    private IEnumerator Emit()
    {
        var wait = new WaitForSeconds(Interval);
        do
        {
            Spawn();
            yield return wait;
        } while (player.IsDashing);
        routine = null;
    }

    private void Spawn()
    {
        if (alive.Count >= MaxAlive)
        {
            Release(alive[0]);
            alive.RemoveAt(0);
        }

        var mesh = new Mesh();
        skinnedRenderer.BakeMesh(mesh, false);
        var t = skinnedRenderer.transform;
        var go = new GameObject("Afterimage");
        go.transform.SetPositionAndRotation(t.position, t.rotation);
        go.transform.localScale = t.lossyScale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = ghostMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var ghost = new Ghost { Go = go, Mesh = mesh, Renderer = mr };
        ghost.Block.SetColor(BaseColorId, ghostColor);
        mr.SetPropertyBlock(ghost.Block);
        alive.Add(ghost);
    }

    private void Update()
    {
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            var g = alive[i];
            g.Age += Time.deltaTime;
            if (g.Age >= FadeTime)
            {
                Release(g);
                alive.RemoveAt(i);
                continue;
            }
            var c = ghostColor;
            c.a *= 1f - g.Age / FadeTime;
            g.Block.SetColor(BaseColorId, c);
            g.Renderer.SetPropertyBlock(g.Block);
        }
    }

    private static void Release(Ghost g)
    {
        if (g.Mesh != null) Destroy(g.Mesh);
        if (g.Go != null) Destroy(g.Go);
    }
}
