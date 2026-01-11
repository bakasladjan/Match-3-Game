using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public bool inputLocked = false;
    public ScoreManager scoreManager;
    public LevelGoalManager levelGoal;

    public int width = 8;
    public int height = 8;

    [Header("Plane Settings")]
    public float planeFlightDuration = 0.25f;
    public int discoPlaneMaxFlights = 12; // cap da ne poludi kad ima 40 gemova iste boje


    [Header("Gem Settings")]
    public float gemScale = 0.3f;

    [Header("Speeds")]
    public float swapDuration = 0.15f;
    public float dropBaseDuration = 0.12f;
    public float spawnDropExtraHeight = 1f;

    [Header("Prefabs / Sprites")]
    public GameObject gemPrefab;
    public Sprite[] gemSprites;

    [Header("Special Sprites")]
    public Sprite bombSprite;
    public Sprite rocketHSprite;
    public Sprite rocketVSprite;
    public Sprite discoBallSprite;
    public Sprite paperPlaneSprite;

    [Header("Particles (Prefabs)")]
    public ParticleSystem bombFXPrefab;
    public ParticleSystem rocketHFXPrefab;
    public ParticleSystem rocketVFXPrefab;
    public ParticleSystem discoFXPrefab;
    public ParticleSystem createSpecialFXPrefab;
    public ParticleSystem paperPlaneFXPrefab;

    private Gem[,] gems;
    private bool isShifting = false;

    private Gem lastSwapA;
    private Gem lastSwapB;
    public static BoardManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        gems = new Gem[width, height];
        StartCoroutine(SetupBoard());
    }
    public void TryActivateSpecial(Gem g)
    {
        if (g == null) return;
        if (inputLocked) return;
        if (GameManager.Instance.isGameOver) return;
        if (isShifting) return;
        if (!g.IsSpecial) return;

        StartCoroutine(ActivateSpecialByTap(g));
    }

    IEnumerator ActivateSpecialByTap(Gem g)
    {
        isShifting = true;

        // Aktiviraj kao da je "single special on swap"
        ActivateSingleSpecialOnSwap(g);

        // ako je avion, sačekaj da udari metu pre rušenja table
        yield return new WaitForSeconds(planeFlightDuration + 0.05f);

        if (levelGoal != null) levelGoal.RegisterMoveUsed();
        if (scoreManager != null) scoreManager.IncreaseCombo();

        yield return StartCoroutine(DestroyMarkedAndCollapse());

        // DestroyMarkedAndCollapse vodi dalje u cascade, ovde samo otključaj ako treba
        isShifting = false;
    }

    IEnumerator SetupBoard()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                SpawnNewGemAt(x, y, animate: false);

        while (FindAllMatchesAndMarkIncludingSquares().Count > 0)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    if (gems[x, y] != null && gems[x, y].isMatched)
                    {
                        int newType = Random.Range(0, gemSprites.Length);
                        gems[x, y].SetType(newType, gemSprites[newType]);
                        gems[x, y].SetSpecial(Gem.SpecialType.None, null);
                        gems[x, y].isMatched = false;
                    }
                }
        }


        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                Gem g = gems[x, y];
                Vector3 startPos = CoordToWorld(x, y + height);
                Vector3 endPos = CoordToWorld(x, y);

                g.transform.position = startPos;
                g.transform.localScale = Vector3.zero;

                StartCoroutine(MoveGem(g, endPos, dropBaseDuration * height, withBounce: true));
                StartCoroutine(g.AnimateSpawn());
            }

        yield return null;
    }


    void SpawnNewGemAt(int x, int y, bool animate = true)
    {
        Vector3 spawnPos = CoordToWorld(x, y + height + (int)spawnDropExtraHeight);
        GameObject obj = Instantiate(gemPrefab, spawnPos, Quaternion.identity, transform);
        Gem gem = obj.GetComponent<Gem>();

        int t = Random.Range(0, gemSprites.Length);
        gem.SetType(t, gemSprites[t]);
        gem.SetSpecial(Gem.SpecialType.None, null);

        gem.x = x;
        gem.y = y;
        gems[x, y] = gem;

        if (animate)
        {
            Vector3 target = CoordToWorld(x, y);
            gem.transform.localScale = Vector3.zero;
            StartCoroutine(MoveGem(gem, target, dropBaseDuration * (height - y), withBounce: true));
            StartCoroutine(gem.AnimateSpawn());
        }
        else
        {
            gem.transform.position = CoordToWorld(x, y);
            gem.transform.localScale = Vector3.one * gemScale;
        }
    }

    Vector3 CoordToWorld(int x, int y)
    {
        return new Vector3(x - width / 2f + 0.5f, y - height / 2f + 0.5f, 0);
    }

    IEnumerator MoveGem(Gem gem, Vector3 target, float duration = 0.15f, bool withBounce = false)
    {
        if (gem == null || gem.isBeingDestroyed) yield break;

        Vector3 start = gem.transform.position;
        float t = 0f;

        while (t < 1f)
        {
            if (gem == null || gem.isBeingDestroyed) yield break;

            t += Time.deltaTime / duration;
            float eased = Mathf.Sin(t * Mathf.PI * 0.5f);
            gem.transform.position = Vector3.Lerp(start, target, eased);
            yield return null;
        }

        if (gem == null || gem.isBeingDestroyed) yield break;

        gem.transform.position = target;

        if (withBounce)
            yield return StartCoroutine(gem.Bounce());
    }

    // -------------------- SWAP --------------------

    public IEnumerator TrySwap(Gem a, Gem b)
    {
        Debug.Log($"SWAP CHECK: A({a.x},{a.y}) special={a.specialType} IsSpecial={a.IsSpecial} | B({b.x},{b.y}) special={b.specialType} IsSpecial={b.IsSpecial}");

        if (inputLocked) yield break;
        if (GameManager.Instance.isGameOver) yield break;
        if (isShifting) yield break;

        discosActivatedBySwap.Clear();

        isShifting = true;

        lastSwapA = a;
        lastSwapB = b;

        SwapInArray(a, b);

        Vector3 posA = CoordToWorld(a.x, a.y);
        Vector3 posB = CoordToWorld(b.x, b.y);
        yield return StartCoroutine(SwapAnimation(a, b, posA, posB));

        // Specijal swap = validan potez
        if ((a != null && a.IsSpecial) || (b != null && b.IsSpecial))
        {
            yield return StartCoroutine(ActivateSpecialSwap(a, b));

            // da plane stigne da udari metu pre destroy-a
            yield return new WaitForSeconds(planeFlightDuration + 0.05f);

            if (levelGoal != null) levelGoal.RegisterMoveUsed();
            if (scoreManager != null) scoreManager.IncreaseCombo();

            yield return StartCoroutine(DestroyMarkedAndCollapse());
            yield break;
        }



        var matches = FindAllMatchesAndMarkIncludingSquares();
        if (matches.Count == 0)
        {
            SwapInArray(a, b);

            Vector3 backA = CoordToWorld(a.x, a.y);
            Vector3 backB = CoordToWorld(b.x, b.y);
            yield return StartCoroutine(SwapAnimation(a, b, backA, backB));

            isShifting = false;
            if (scoreManager != null) scoreManager.ResetCombo();
            yield break;
        }

        if (levelGoal != null) levelGoal.RegisterMoveUsed();
        if (scoreManager != null) scoreManager.IncreaseCombo();

        yield return StartCoroutine(DestroyMatchesAndCollapse());
    }
    void MarkAll2x2Squares()
    {
        for (int x = 0; x < width - 1; x++)
        {
            for (int y = 0; y < height - 1; y++)
            {
                Gem a = gems[x, y];
                Gem b = gems[x + 1, y];
                Gem c = gems[x, y + 1];
                Gem d = gems[x + 1, y + 1];
                if (a == null || b == null || c == null || d == null) continue;
                if (a.IsSpecial || b.IsSpecial || c.IsSpecial || d.IsSpecial) continue;
                if (!IsMatchable(a) || !IsMatchable(b) || !IsMatchable(c) || !IsMatchable(d)) continue;
                int t = a.type;
                if (b.type == t && c.type == t && d.type == t)
                {
                    a.isMatched = true;
                    b.isMatched = true;
                    c.isMatched = true;
                    d.isMatched = true;
                }
            }
        }
    }

    void Update()
    {
        // TEST: pritisni D da napraviš disko kuglu u centru
        if (Input.GetKeyDown(KeyCode.D))
        {
            int cx = width / 2;
            int cy = height / 2;

            Gem g = GetGemAt(cx, cy);
            if (g != null)
            {
                g.SetSpecial(Gem.SpecialType.DiscoBall, discoBallSprite);
                Debug.Log($"TEST DISCO CREATED at ({cx},{cy})");
            }
        }
    }
    Gem PickPlaneTargetUnique(bool avoidSpecials = false, int tries = 30)
    {
        Gem pick = null;

        for (int i = 0; i < tries; i++)
        {
            pick = PickPlaneTarget(avoidSpecials);
            if (pick == null) return null;

            if (!reservedPlaneTargets.Contains(pick))
            {
                reservedPlaneTargets.Add(pick);
                return pick;
            }
        }

        // fallback (ako nema više)
        return pick;
    }

    IEnumerator DestroyMarkedAndCollapse()
    {
        planesLaunchedThisAction.Clear();
        reservedPlaneTargets.Clear();


        // 1) Sakupi sve što je već markirano (bez resetovanja)
        List<Gem> matched = new List<Gem>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (gems[x, y] != null && gems[x, y].isMatched)
                    matched.Add(gems[x, y]);

        if (matched.Count == 0)
            yield break;

        // ===============================
        // 🔥 CHAIN REACTION SPECIJALA (za specijal swap / pre-destroy)
        // ===============================
        activatedThisWave.Clear();
        activationQueue.Clear();

        // enqueue svih specijala koji su već pogođeni (isMatched)
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Gem g = gems[x, y];
                if (g == null) continue;
                if (!g.isMatched) continue;
                EnqueueSpecialIfNeeded(g);
            }
        }

        // aktiviraj lančane reakcije
        ResolveSpecialChainReactions();

        // ✅ ponovo sakupi matched posle chain-a
        matched.Clear();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (gems[x, y] != null && gems[x, y].isMatched)
                    matched.Add(gems[x, y]);

        if (matched.Count == 0)
            yield break;

        // scoring
        if (scoreManager != null)
        {
            scoreManager.RegisterMatch(matched);
            scoreManager.AddScore(matched.Count);
        }

        // FX
        foreach (var g in matched)
            if (g != null && g.IsSpecial)
                PlaySpecialFX(g);

        // 2) Uništi markirane
        foreach (var g in matched)
        {
            if (g == null) continue;
            StartCoroutine(DestroyGemAnimated(g));
            gems[g.x, g.y] = null;
        }

        yield return new WaitForSeconds(0.25f);

        // 3) Collapse + spawn
        for (int x = 0; x < width; x++)
        {
            List<Gem> column = new List<Gem>();
            for (int y = 0; y < height; y++)
                if (gems[x, y] != null)
                    column.Add(gems[x, y]);

            for (int y = 0; y < column.Count; y++)
            {
                Gem g = column[y];
                int newY = y;
                gems[x, newY] = g;

                if (g.y != newY)
                {
                    g.y = newY;
                    StartCoroutine(MoveGem(g, CoordToWorld(x, newY),
                        dropBaseDuration * (height - newY),
                        withBounce: true));
                }
            }

            for (int y = column.Count; y < height; y++)
                SpawnNewGemAt(x, y, animate: true);
        }

        yield return new WaitForSeconds(0.3f);

        // 4) Nastavi normalne cascade match-eve
        yield return StartCoroutine(DestroyMatchesAndCollapse());
    }


    void SwapInArray(Gem a, Gem b)
    {
        int ax = a.x, ay = a.y;
        int bx = b.x, by = b.y;

        gems[ax, ay] = b;
        gems[bx, by] = a;

        a.x = bx; a.y = by;
        b.x = ax; b.y = ay;
    }

    IEnumerator SwapAnimation(Gem a, Gem b, Vector3 targetA, Vector3 targetB)
    {
        Coroutine ca = StartCoroutine(MoveGem(a, targetA, swapDuration, withBounce: true));
        Coroutine cb = StartCoroutine(MoveGem(b, targetB, swapDuration, withBounce: true));
        yield return ca;
        yield return cb;
    }

    // -------------------- MATCH --------------------

    struct MatchLine
    {
        public List<Gem> gems;
        public bool horizontal;
        public int length;
        public int type;
    }

    List<Gem> FindAllMatchesAndMark()
    {
        List<Gem> matched = new List<Gem>();

        // reset
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (gems[x, y] != null)
                    gems[x, y].isMatched = false;

        // ---------- Horizontal ----------
        for (int y = 0; y < height; y++)
        {
            int x = 0;
            while (x < width - 2)
            {
                // 🔥 KLJUČ: preskoči null i specijale
                if (!IsMatchable(gems[x, y])) { x++; continue; }

                int t = gems[x, y].type;
                int run = 1;

                // 🔥 KLJUČ: i nastavak run-a mora da bude matchable
                while (x + run < width &&
                       IsMatchable(gems[x + run, y]) &&
                       gems[x + run, y].type == t)
                {
                    run++;
                }

                if (run >= 3)
                {
                    for (int i = 0; i < run; i++)
                        gems[x + i, y].isMatched = true;
                }

                x += run;
            }
        }

        // ---------- Vertical ----------
        for (int x = 0; x < width; x++)
        {
            int y = 0;
            while (y < height - 2)
            {
                // 🔥 KLJUČ: preskoči null i specijale
                if (!IsMatchable(gems[x, y])) { y++; continue; }

                int t = gems[x, y].type;
                int run = 1;

                while (y + run < height &&
                       IsMatchable(gems[x, y + run]) &&
                       gems[x, y + run].type == t)
                {
                    run++;
                }

                if (run >= 3)
                {
                    for (int i = 0; i < run; i++)
                        gems[x, y + i].isMatched = true;
                }

                y += run;
            }
        }

        // collect
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (gems[x, y] != null && gems[x, y].isMatched)
                    matched.Add(gems[x, y]);

        return matched;
    }


    List<MatchLine> GetMatchedLinesFromMarks()
    {
        List<MatchLine> lines = new List<MatchLine>();

        // Horizontal matched lines
        for (int y = 0; y < height; y++)
        {
            int x = 0;
            while (x < width)
            {
                Gem g = gems[x, y];
                if (g == null || !g.isMatched || g.IsSpecial) { x++; continue; }

                int t = g.type;
                List<Gem> run = new List<Gem>();

                int xx = x;
                while (xx < width)
                {
                    Gem gg = gems[xx, y];
                    if (gg == null || !gg.isMatched || gg.IsSpecial || gg.type != t) break;
                    run.Add(gg);
                    xx++;
                }

                if (run.Count >= 3)
                    lines.Add(new MatchLine { gems = run, horizontal = true, length = run.Count, type = t });

                x = xx;
            }
        }

        // Vertical matched lines
        for (int x = 0; x < width; x++)
        {
            int y = 0;
            while (y < height)
            {
                Gem g = gems[x, y];
                if (g == null || !g.isMatched || g.IsSpecial) { y++; continue; }

                int t = g.type;
                List<Gem> run = new List<Gem>();

                int yy = y;
                while (yy < height)
                {
                    Gem gg = gems[x, yy];
                    if (gg == null || !gg.isMatched || gg.IsSpecial || gg.type != t) break;
                    run.Add(gg);
                    yy++;
                }

                if (run.Count >= 3)
                    lines.Add(new MatchLine { gems = run, horizontal = false, length = run.Count, type = t });

                y = yy;
            }
        }

        return lines;
    }
    Gem FindLTBombHostFromMarks()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Gem c = gems[x, y];
                if (c == null) continue;
                if (!c.isMatched) continue;
                if (c.IsSpecial) continue;

                int t = c.type;

                int left = 0;
                while (x - (left + 1) >= 0)
                {
                    Gem g = gems[x - (left + 1), y];
                    if (g == null || !g.isMatched || g.IsSpecial || g.type != t) break;
                    left++;
                }

                int right = 0;
                while (x + (right + 1) < width)
                {
                    Gem g = gems[x + (right + 1), y];
                    if (g == null || !g.isMatched || g.IsSpecial || g.type != t) break;
                    right++;
                }

                int down = 0;
                while (y - (down + 1) >= 0)
                {
                    Gem g = gems[x, y - (down + 1)];
                    if (g == null || !g.isMatched || g.IsSpecial || g.type != t) break;
                    down++;
                }

                int up = 0;
                while (y + (up + 1) < height)
                {
                    Gem g = gems[x, y + (up + 1)];
                    if (g == null || !g.isMatched || g.IsSpecial || g.type != t) break;
                    up++;
                }

                if ((left + 1 + right) >= 3 && (down + 1 + up) >= 3)
                    return c;
            }
        }
        return null;
    }

    // -------------------- SPECIAL CREATION (FIXED) --------------------
    // Ključ fix-a: vratimo host i zaštitimo ga da ne bude destroyed.

    Gem ChooseSpecialHost(List<Gem> line)
    {
        if (line == null || line.Count == 0) return null;

        // Prioritet: gem koji je stvarno pomeren u swapu
        if (lastSwapA != null)
            foreach (var g in line)
                if (g == lastSwapA) return g;

        if (lastSwapB != null)
            foreach (var g in line)
                if (g == lastSwapB) return g;

        // fallback: krajnji (Homescapes često pravi na kraju linije)
        return line[line.Count - 1];
    }


    Gem CreateSpecialFromLine(MatchLine line)
    {
        if (line.gems == null || line.gems.Count < 4) return null;

        Gem host = ChooseSpecialHost(line.gems);
        if (host == null) return null;
        if (host.IsSpecial) return null;

        // 5+ -> DISCO
        if (line.length >= 5)
        {
            host.SetSpecial(Gem.SpecialType.DiscoBall, discoBallSprite);
            SpawnFX(createSpecialFXPrefab, host.transform.position);
            return host;
        }

        // 4 -> Rocket
        if (line.length == 4)
        {
            if (line.horizontal)
                host.SetSpecial(Gem.SpecialType.RocketH, rocketHSprite);
            else
                host.SetSpecial(Gem.SpecialType.RocketV, rocketVSprite);

            SpawnFX(createSpecialFXPrefab, host.transform.position);
            return host;
        }

        return null;
    }

    // -------------------- FX helpers --------------------

    void SpawnFX(ParticleSystem fxPrefab, Vector3 pos)
    {
        if (fxPrefab == null) return;
        var fx = Instantiate(fxPrefab, pos, Quaternion.identity);
        Destroy(fx.gameObject, 2.5f);
    }

    void PlaySpecialFX(Gem g)
    {
        if (g == null) return;

        if (g.specialType == Gem.SpecialType.Bomb) SpawnFX(bombFXPrefab, g.transform.position);
        else if (g.specialType == Gem.SpecialType.RocketH) SpawnFX(rocketHFXPrefab, g.transform.position);
        else if (g.specialType == Gem.SpecialType.RocketV) SpawnFX(rocketVFXPrefab, g.transform.position);
        else if (g.specialType == Gem.SpecialType.DiscoBall) SpawnFX(discoFXPrefab, g.transform.position);
        else if (g.specialType == Gem.SpecialType.PaperPlane) SpawnFX(paperPlaneFXPrefab, g.transform.position);

    }

    // -------------------- SPECIAL TARGETS --------------------

    List<Gem> GetSpecialTargets(Gem g)
    {
        List<Gem> targets = new List<Gem>();
        if (g == null) return targets;

        if (g.specialType == Gem.SpecialType.RocketH)
        {
            for (int x = 0; x < width; x++)
                if (gems[x, g.y] != null) targets.Add(gems[x, g.y]);
        }
        else if (g.specialType == Gem.SpecialType.RocketV)
        {
            for (int y = 0; y < height; y++)
                if (gems[g.x, y] != null) targets.Add(gems[g.x, y]);
        }
        else if (g.specialType == Gem.SpecialType.Bomb)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    Gem t = GetGemAt(g.x + dx, g.y + dy);
                    if (t != null) targets.Add(t);
                }
        }

        return targets;
    }

    void ExpandMatchesBySpecials()
    {
        bool added;
        int guard = 0;

        do
        {
            added = false;
            guard++;
            if (guard > 20) break;

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    Gem g = gems[x, y];
                    if (g == null) continue;
                    if (!g.isMatched) continue;
                    if (!g.IsSpecial) continue;

                    // Disco se aktivira swap-om (ne match-om)
                    if (g.specialType == Gem.SpecialType.DiscoBall) continue;

                    foreach (var t in GetSpecialTargets(g))
                    {
                        if (t == null) continue;
                        if (!t.isMatched)
                        {
                            t.isMatched = true;
                            added = true;
                        }
                    }
                }
        } while (added);
    }

    // -------------------- DISCO HELPERS --------------------

    void MarkAllOfType(int type, bool includeSpecials)
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                Gem g = gems[x, y];
                if (g == null) continue;
                if (g.type != type) continue;

                if (!includeSpecials && g.IsSpecial) continue; // <--- KLJUČ

                g.isMatched = true;
            }
    }


    void MarkAllBoard()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (gems[x, y] != null)
                    gems[x, y].isMatched = true;
    }

    IEnumerator ActivateSpecialSwap(Gem a, Gem b)
    {
        if (a == null || b == null) yield break;

        bool aDisco = a.specialType == Gem.SpecialType.DiscoBall;
        bool bDisco = b.specialType == Gem.SpecialType.DiscoBall;

        bool aPlane = a.specialType == Gem.SpecialType.PaperPlane;
        bool bPlane = b.specialType == Gem.SpecialType.PaperPlane;

        bool aRocket = (a.specialType == Gem.SpecialType.RocketH || a.specialType == Gem.SpecialType.RocketV);
        bool bRocket = (b.specialType == Gem.SpecialType.RocketH || b.specialType == Gem.SpecialType.RocketV);

        bool aBomb = (a.specialType == Gem.SpecialType.Bomb);
        bool bBomb = (b.specialType == Gem.SpecialType.Bomb);

        // --- DISCO + DISCO
        if (aDisco && bDisco)
        {
            // ✅ oba su swap-aktivirana, ne smeju posle da uđu u chain kao "random"
            discosActivatedBySwap.Add(a);
            discosActivatedBySwap.Add(b);

            PlaySpecialFX(a);
            PlaySpecialFX(b);
            MarkAllBoard();
            yield break;
        }

        // ✅✅✅ DISCO + PLANE (MORA PRE "DISCO + anything")
        if ((aDisco && bPlane) || (bDisco && aPlane))
        {
            Gem disco = aDisco ? a : b;
            Gem plane = aPlane ? a : b;

            // ✅ disco je swap-aktiviran
            discosActivatedBySwap.Add(disco);

            // ✅ reset za ovaj "potez" (da ne vuku stare rezervacije)
            planesLaunchedThisAction.Clear();
            reservedPlaneTargets.Clear();

            PlaySpecialFX(disco);

            int targetType = plane.type;

            // pokupi sve gemove te boje (do cap)
            List<Gem> planesToLaunch = new List<Gem>();
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    Gem g = gems[x, y];
                    if (g == null) continue;
                    if (g.isBeingDestroyed) continue;
                    if (g.type != targetType) continue;
                    if (g.IsSpecial) continue; // ✅ NOVO
                    planesToLaunch.Add(g);

                }

            // limit
            if (planesToLaunch.Count > discoPlaneMaxFlights)
                planesToLaunch.RemoveRange(discoPlaneMaxFlights, planesToLaunch.Count - discoPlaneMaxFlights);

            // pretvori ih u plane i lansiraj (sa UNIQUE metama)
            foreach (var p in planesToLaunch)
            {
                if (p == null) continue;
                if (p.isBeingDestroyed) continue;

                // ✅ ako je već lansiran (npr. pogođen tuđim +), preskoči
                if (planesLaunchedThisAction.Contains(p))
                    continue;

                planesLaunchedThisAction.Add(p);

                p.SetSpecial(Gem.SpecialType.PaperPlane, paperPlaneSprite);

                // ✅ unique target globalno u okviru poteza
                Gem t = PickPlaneTargetReserved(reservedPlaneTargets, avoidSpecials: false);


                StartCoroutine(FlyPlaneAndHit(p, t, (hit) =>
                {
                    if (hit == null) return;
                    hit.isMatched = true;

                    // ✅ ako je specijal pogođen na meti, ubaci ga u chain
                    EnqueueSpecialIfNeeded(hit);
                }));
            }

            // potroši swap par
            disco.isMatched = true;
            plane.isMatched = true;

            yield break;
        }



        // --- DISCO + anything
        if (aDisco)
        {
            discosActivatedBySwap.Add(a);
            yield return StartCoroutine(ActivateDiscoCombo(a, b));
            yield break;
        }
        if (bDisco)
        {
            discosActivatedBySwap.Add(b);
            yield return StartCoroutine(ActivateDiscoCombo(b, a));
            yield break;
        }

        // ---------------- PLANE + PLANE => 2 mete
        if (aPlane && bPlane)
        {
            // ✅ oba odmah označi kao lansirana (da se ne “pokupe” međusobno u +)
            planesLaunchedThisAction.Add(a);
            planesLaunchedThisAction.Add(b);

            // ✅ unique mete
            Gem t1 = PickPlaneTargetUnique(avoidSpecials: false);
            Gem t2 = PickPlaneTargetUnique(avoidSpecials: false);

            StartCoroutine(FlyPlaneAndHit(a, t1, (hit) =>
            {
                if (hit != null) hit.isMatched = true;
            }));

            StartCoroutine(FlyPlaneAndHit(b, t2, (hit) =>
            {
                if (hit != null) hit.isMatched = true;
            }));

            yield break;
        }

        // ---------------- PLANE + ROCKET => hit meta pa rocket blast na meti
        if ((aPlane && bRocket) || (bPlane && aRocket))
        {
            Gem plane = aPlane ? a : b;
            Gem rocket = aRocket ? a : b;

            Gem target = PickPlaneTarget();

            StartCoroutine(FlyPlaneAndHit(plane, target, (hit) =>
            {
                if (hit == null) return;

                hit.isMatched = true;

                bool makeH = Random.value > 0.5f;
                if (makeH)
                {
                    for (int x = 0; x < width; x++)
                        if (gems[x, hit.y] != null) gems[x, hit.y].isMatched = true;
                }
                else
                {
                    for (int y = 0; y < height; y++)
                        if (gems[hit.x, y] != null) gems[hit.x, y].isMatched = true;
                }
            }));

            rocket.isMatched = true;
            yield break;
        }

        // ---------------- PLANE + BOMB => hit meta pa 3x3 eksplozija na meti
        if ((aPlane && bBomb) || (bPlane && aBomb))
        {
            Gem plane = aPlane ? a : b;
            Gem bomb = aBomb ? a : b;

            Gem target = PickPlaneTarget();

            StartCoroutine(FlyPlaneAndHit(plane, target, (hit) =>
            {
                if (hit == null) return;

                hit.isMatched = true;

                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        Gem t = GetGemAt(hit.x + dx, hit.y + dy);
                        if (t != null) t.isMatched = true;
                    }
            }));

            bomb.isMatched = true;
            yield break;
        }

        // =========================
        // SPECIAL + SPECIAL COMBOS
        // =========================
        int cx = a.x;
        int cy = a.y;

        if (aRocket && bRocket)
        {
            PlaySpecialFX(a);
            PlaySpecialFX(b);

            Mark3Rows3Cols(cx, cy);

            a.isMatched = true;
            b.isMatched = true;
            yield break;
        }

        if (aBomb && bBomb)
        {
            PlaySpecialFX(a);
            PlaySpecialFX(b);

            MarkSquare(cx, cy, radius: 2);

            a.isMatched = true;
            b.isMatched = true;
            yield break;
        }

        if ((aBomb && bRocket) || (aRocket && bBomb))
        {
            PlaySpecialFX(a);
            PlaySpecialFX(b);

            MarkThickPlus(cx, cy, thicknessRadius: 1);

            a.isMatched = true;
            b.isMatched = true;
            yield break;
        }

        // =========================
        // SINGLE SPECIAL on swap
        // =========================
        if (a.IsSpecial) ActivateSingleSpecialOnSwap(a);
        if (b.IsSpecial) ActivateSingleSpecialOnSwap(b);

        yield return null;
    }

    Gem Find2x2SquareHost()
    {
        for (int x = 0; x < width - 1; x++)
        {
            for (int y = 0; y < height - 1; y++)
            {
                Gem a = gems[x, y];
                Gem b = gems[x + 1, y];
                Gem c = gems[x, y + 1];
                Gem d = gems[x + 1, y + 1];
                if (a == null || b == null || c == null || d == null) continue;

                int t = a.type;
                if (b.type != t || c.type != t || d.type != t) continue;

                // ✅ 2x2 važi SAMO ako je trenutno markiran kao match (u ovom ciklusu)
                if (!(a.isMatched && b.isMatched && c.isMatched && d.isMatched)) continue;

                // ✅ ne pravimo aviončić ako bilo koji od 4 je specijalan
                if (a.IsSpecial || b.IsSpecial || c.IsSpecial || d.IsSpecial) continue;

                // host: prioritet svajpovani ako je u kvadratu, ali ako nije — i dalje napravimo aviončić (cascade)
                if (lastSwapA == a || lastSwapA == b || lastSwapA == c || lastSwapA == d) return lastSwapA;
                if (lastSwapB == a || lastSwapB == b || lastSwapB == c || lastSwapB == d) return lastSwapB;

                return d; // fallback
            }
        }
        return null;
    }




    void MarkRow(int y)
    {
        if (y < 0 || y >= height) return;
        for (int x = 0; x < width; x++)
            if (gems[x, y] != null)
                gems[x, y].isMatched = true;
    }

    void MarkCol(int x)
    {
        if (x < 0 || x >= width) return;
        for (int y = 0; y < height; y++)
            if (gems[x, y] != null)
                gems[x, y].isMatched = true;
    }

    // 3 reda + 3 kolone (Homescapes-like rocket+rocket)
    void Mark3Rows3Cols(int cx, int cy)
    {
        for (int dy = -1; dy <= 1; dy++)
            MarkRow(cy + dy);

        for (int dx = -1; dx <= 1; dx++)
            MarkCol(cx + dx);
    }

    // 5x5 (bomb+bomb)
    void MarkSquare(int cx, int cy, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                Gem g = GetGemAt(cx + dx, cy + dy);
                if (g != null) g.isMatched = true;
            }
    }

    // Plus debljine 3 (bomb+rocket)
    void MarkThickPlus(int cx, int cy, int thicknessRadius)
    {
        // 3 reda + 3 kolone, ali centrirano na jednu tačku
        for (int dy = -thicknessRadius; dy <= thicknessRadius; dy++)
            MarkRow(cy + dy);

        for (int dx = -thicknessRadius; dx <= thicknessRadius; dx++)
            MarkCol(cx + dx);
    }

    IEnumerator ActivateDiscoCombo(Gem disco, Gem other)
    {
        if (disco == null) yield break;
        discosActivatedBySwap.Add(disco);
        // FX za disko uvek
        PlaySpecialFX(disco);

        // ako je other null (ne bi trebalo), samo ukloni disko
        if (other == null)
        {
            disco.isMatched = true;
            yield break;
        }

        int targetType = other.type;

        // --- DISCO + ROCKET  => svi targetType postanu ROCKET i aktiviraju se
        if (other.specialType == Gem.SpecialType.RocketH || other.specialType == Gem.SpecialType.RocketV)
        {
            TransformAllOfTypeToSpecial(targetType, makeBomb: false, makeRocket: true);

            // Markiraj sve te boje (sad su rakete) da se unište
            MarkAllOfType(targetType, includeSpecials: true);


            // Proširi markiranje preko raketa (red/kolona)
            ExpandMatchesBySpecials();

            // Ukloni i swap par
            disco.isMatched = true;
            other.isMatched = true;

            // FX i za other
            PlaySpecialFX(other);

            yield break;
        }

        // --- DISCO + BOMB => svi targetType postanu BOMBE i aktiviraju se
        if (other.specialType == Gem.SpecialType.Bomb)
        {
            TransformAllOfTypeToSpecial(targetType, makeBomb: true, makeRocket: false);

            // Markiraj sve te boje (sad su bombe)
            MarkAllOfType(targetType, includeSpecials: true);


            // Proširi markiranje preko bombi (3x3)
            ExpandMatchesBySpecials();

            // Ukloni i swap par
            disco.isMatched = true;
            other.isMatched = true;

            PlaySpecialFX(other);

            yield break;
        }

        // --- DISCO + NORMAL GEM => remove all of that color (ali NE specijale)
        MarkAllOfType(targetType, includeSpecials: false);

        disco.isMatched = true;
        other.isMatched = true;
        yield return null;

    }
    bool IsMatchable(Gem g)
    {
        return g != null && !g.IsSpecial && !g.isBeingDestroyed;
    }

    void TransformAllOfTypeToSpecial(int targetType, bool makeBomb, bool makeRocket)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Gem g = gems[x, y];
                if (g == null) continue;
                if (g.type != targetType) continue;

                // Ne diraj DISCO kugle (ako ih ima)
                if (g.specialType == Gem.SpecialType.DiscoBall) continue;

                if (makeBomb)
                {
                    g.SetSpecial(Gem.SpecialType.Bomb, bombSprite);
                    continue;
                }

                if (makeRocket)
                {
                    bool makeH = Random.value > 0.5f;
                    g.SetSpecial(makeH ? Gem.SpecialType.RocketH : Gem.SpecialType.RocketV,
                                 makeH ? rocketHSprite : rocketVSprite);
                }
            }
        }
    }


    void ActivateSingleSpecialOnSwap(Gem g)
    {
        if (g == null) return;

        if (g.specialType == Gem.SpecialType.PaperPlane)
        {
            Gem target = PickPlaneTarget(avoidSpecials: false);

            StartCoroutine(FlyPlaneAndHit(g, target, (hit) =>
            {
                if (hit != null) hit.isMatched = true;
            }));

            return;
        }

        if (g.specialType == Gem.SpecialType.RocketH)
        {
            for (int x = 0; x < width; x++)
                if (gems[x, g.y] != null)
                    gems[x, g.y].isMatched = true;

            g.isMatched = true;
            PlaySpecialFX(g);
            return;
        }

        if (g.specialType == Gem.SpecialType.RocketV)
        {
            for (int y = 0; y < height; y++)
                if (gems[g.x, y] != null)
                    gems[g.x, y].isMatched = true;

            g.isMatched = true;
            PlaySpecialFX(g);
            return;
        }

        if (g.specialType == Gem.SpecialType.Bomb)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    Gem t = GetGemAt(g.x + dx, g.y + dy);
                    if (t != null) t.isMatched = true;
                }

            g.isMatched = true;
            PlaySpecialFX(g);
            return;
        }
    }



    // -------------------- DESTROY + COLLAPSE (FIXED) --------------------

    // ===================== DESTROY + COLLAPSE (STABILNA VERZIJA) =====================
    // Ideja:
    // - Jednom markiraš (linije + 2x2) => to je "istina" za ovaj ciklus
    // - Na osnovu TOG markiranja napraviš specijal (bomb/rocket/disco/plane)
    // - Host specijala skineš iz destruction-a (isMatched=false)
    // - Ne radiš ponovni FindAllMatches... pre rušenja (to je bio izvor bugova)
    // - Posle collapse-a tek ponovo tražiš nove matcheve (kaskada)

    IEnumerator DestroyMatchesAndCollapse()
    {
        List<Gem> matched = FindAllMatchesAndMarkIncludingSquares();

        while (matched.Count > 0)
        {
            HashSet<Gem> protectedSpecials = new HashSet<Gem>();

            // 1) Specijale kreiramo isključivo iz ONOGA što je već markirano u ovom ciklusu
            var lines = GetMatchedLinesFromMarks();              // 3+ linije koje su već isMatched
            Gem bombHost = FindLTBombHostFromMarks();           // L/T iz markiranih
            Gem planeHost = Find2x2SquareHost();                 // 2x2 iz markiranih (tvoja verzija sa isMatched check)

            // 1.1) L/T bomba
            if (bombHost != null && !bombHost.IsSpecial)
            {
                bombHost.SetSpecial(Gem.SpecialType.Bomb, bombSprite);
                SpawnFX(createSpecialFXPrefab, bombHost.transform.position);
                protectedSpecials.Add(bombHost);
            }

            // 1.2) 2x2 aviončić
            if (planeHost != null && !planeHost.IsSpecial)
            {
                planeHost.SetSpecial(Gem.SpecialType.PaperPlane, paperPlaneSprite);
                SpawnFX(createSpecialFXPrefab, planeHost.transform.position);
                protectedSpecials.Add(planeHost);
            }

            // 1.3) 4/5 linije => rocket/disco
            foreach (var line in lines)
            {
                if (line.length >= 4)
                {
                    Gem host = CreateSpecialFromLine(line);
                    if (host != null) protectedSpecials.Add(host);
                }
            }

            // 2) Host specijali ostaju na tabli => skidamo ih iz destruction-a
            foreach (var p in protectedSpecials)
                if (p != null) p.isMatched = false;

            // ✅ 4) Proširi match preko postojećih specijala
            ExpandMatchesBySpecials();


            // ===============================
            // 🔥 CHAIN REACTION SPECIJALA
            // ===============================
            activatedThisWave.Clear();
            activationQueue.Clear();

            // svi već pogođeni specijali ulaze u queue
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Gem g = gems[x, y];
                    if (g == null) continue;
                    if (!g.isMatched) continue;

                    EnqueueSpecialIfNeeded(g);
                }
            }

            // aktiviraj lančane reakcije (raketa → bomba → avion → raketa…)
            ResolveSpecialChainReactions();


            // ===============================
            // ✅ 5) Final collect matched
            // ===============================
            matched.Clear();
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (gems[x, y] != null && gems[x, y].isMatched)
                        matched.Add(gems[x, y]);

            if (matched.Count == 0) break;




            // scoring + FX
            if (scoreManager != null)
            {
                scoreManager.RegisterMatch(matched);
                scoreManager.AddScore(matched.Count);
            }

            foreach (var g in matched)
                if (g != null && g.IsSpecial)
                    PlaySpecialFX(g);

            // 5) Uništi markirane
            foreach (var g in matched)
            {
                if (g == null) continue;
                StartCoroutine(DestroyGemAnimated(g));
                gems[g.x, g.y] = null;
            }

            yield return new WaitForSeconds(0.25f);

            // 6) Collapse + spawn
            for (int x = 0; x < width; x++)
            {
                List<Gem> column = new List<Gem>();
                for (int y = 0; y < height; y++)
                    if (gems[x, y] != null)
                        column.Add(gems[x, y]);

                for (int y = 0; y < column.Count; y++)
                {
                    Gem g = column[y];
                    int newY = y;
                    gems[x, newY] = g;

                    if (g.y != newY)
                    {
                        g.y = newY;
                        StartCoroutine(MoveGem(g, CoordToWorld(x, newY),
                            dropBaseDuration * (height - newY),
                            withBounce: true));
                    }
                }

                for (int y = column.Count; y < height; y++)
                    SpawnNewGemAt(x, y, animate: true);
            }

            yield return new WaitForSeconds(0.3f);

            // 7) Tek sad (posle collapse-a) tražimo nove matcheve za sledeći ciklus (kaskada)
            matched = FindAllMatchesAndMarkIncludingSquares();
        }

        if (scoreManager != null)
            scoreManager.ResetCombo();

        isShifting = false;
        lastSwapA = null;
        lastSwapB = null;
    }
    Gem PickPlaneTarget(bool avoidSpecials = false)
    {
        List<Gem> candidates = new List<Gem>();

        bool needYellow = (levelGoal != null && levelGoal.yellowCount < levelGoal.targetYellow);
        bool needGreen = (levelGoal != null && levelGoal.greenCount < levelGoal.targetGreen);

        // 1) prvo cilj boje, ako treba
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                Gem g = gems[x, y];
                if (g == null) continue;
                if (avoidSpecials && g.IsSpecial) continue;

                if (needYellow && g.type == 5) candidates.Add(g);
                else if (needGreen && g.type == 1) candidates.Add(g);
            }

        // 2) fallback: bilo koji
        if (candidates.Count == 0)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    Gem g = gems[x, y];
                    if (g == null) continue;
                    if (avoidSpecials && g.IsSpecial) continue;
                    candidates.Add(g);
                }
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }


    IEnumerator FlyPlaneAndHit(Gem plane, Gem target, System.Action<Gem> onImpact)
    {
        if (plane == null) yield break;


        // ✅ NAJBITNIJE: odmah označi da je ovaj avion već lansiran
        planesLaunchedThisAction.Add(plane);

        // ✅ SAČUVAJ KOORDINATE POLETANJA PRE nego što menjaš x/y
        int launchX = plane.x;
        int launchY = plane.y;

        // ✅ + pri poletanju: odmah uništi obične, specijale ubaci u chain,
        // a aviončiće u dometu lansiraj (ali samo ako NISU već lansirani)
        AffectPlus3AtLaunch(launchX, launchY, plane);

        // mali delay da se vidi start “eksplozija”
        yield return new WaitForSeconds(0.05f);

        // ✅ odvoji avion od table (posle plus-a)
        if (launchX >= 0 && launchX < width && launchY >= 0 && launchY < height)
            gems[launchX, launchY] = null;

        // avion više ne sme da učestvuje kao normalan gem na tabli
        plane.x = -999;
        plane.y = -999;

        PlaySpecialFX(plane);

        if (target == null)
        {
            yield return StartCoroutine(plane.AnimateDestroy(0.1f));
            if (plane != null) Destroy(plane.gameObject);
            yield break;
        }

        Vector3 startPos = plane.transform.position;
        Vector3 endPos = target.transform.position;

        float t = 0f;
        Vector3 dir = (endPos - startPos);
        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float planeAngleOffset = 0f;

        Quaternion startRot = plane.transform.rotation;
        Quaternion endRot = Quaternion.Euler(0, 0, targetAngle + planeAngleOffset);

        while (t < 1f)
        {
            if (plane == null) yield break;

            t += Time.deltaTime / Mathf.Max(0.05f, planeFlightDuration);
            float eased = Mathf.Sin(t * Mathf.PI * 0.5f);

            plane.transform.position = Vector3.Lerp(startPos, endPos, eased);
            plane.transform.rotation = Quaternion.Slerp(startRot, endRot, eased);

            yield return null;
        }

        if (plane == null) yield break;

        plane.transform.position = endPos;
        plane.transform.rotation = endRot;

        // ✅ na meti uništi samo 1 gem
        if (target != null)
        {
            onImpact?.Invoke(target);

            if (target.IsSpecial)
                EnqueueSpecialIfNeeded(target);
        }

        SpawnFX(paperPlaneFXPrefab, endPos);

        yield return StartCoroutine(plane.AnimateDestroy(0.1f));
        if (plane != null) Destroy(plane.gameObject);
    }
    Gem PickPlaneTargetReserved(HashSet<Gem> reserved, bool avoidSpecials = false)
    {
        List<Gem> candidates = new List<Gem>();

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                Gem g = gems[x, y];
                if (g == null) continue;
                if (g.isMatched) continue;                 // ✅ već označen za uništenje
                if (reserved != null && reserved.Contains(g)) continue; // ✅ već rezervisan
                if (avoidSpecials && g.IsSpecial) continue;

                candidates.Add(g);
            }

        if (candidates.Count == 0) return null;

        Gem picked = candidates[Random.Range(0, candidates.Count)];
        reserved?.Add(picked);
        return picked;
    }
    void AffectPlus3AtLaunch(int cx, int cy, Gem launchingPlane)
    {
        AffectCellAtLaunch(cx, cy, launchingPlane);
        AffectCellAtLaunch(cx - 1, cy, launchingPlane);
        AffectCellAtLaunch(cx + 1, cy, launchingPlane);
        AffectCellAtLaunch(cx, cy - 1, launchingPlane);
        AffectCellAtLaunch(cx, cy + 1, launchingPlane);

        // ❌ NEMA Clear() ovde
        // ❌ NEMA ResolveSpecialChainReactions() ovde
        // Chain će se rešiti kasnije u DestroyMarkedAndCollapse / DestroyMatchesAndCollapse
    }

    void AffectCellAtLaunch(int x, int y, Gem launchingPlane)
    {
        Gem g = GetGemAt(x, y);
        if (g == null) return;
        if (g == launchingPlane) return;
        if (g.isBeingDestroyed) return;

        // ✅ ako je avion u dometu "+" -> NE UNIŠTAVAJ, nego ga lansiraj
        if (g.specialType == Gem.SpecialType.PaperPlane)
        {
            if (planesLaunchedThisAction.Contains(g))
                return;

            planesLaunchedThisAction.Add(g);

            Gem t = PickPlaneTargetUnique(avoidSpecials: false);
            StartCoroutine(FlyPlaneAndHit(g, t, (hit) =>
            {
                if (hit == null) return;
                hit.isMatched = true;
                EnqueueSpecialIfNeeded(hit);
            }));

            return;
        }

        // ✅ specijal pogođen u "+" -> mark + enqueue (da se aktivira u chain-u)
        if (g.IsSpecial)
        {
            g.isMatched = true;
            EnqueueSpecialIfNeeded(g);
            return;
        }

        // ✅ običan gem: odmah uništi (animacija odmah)
        TryDestroyImmediate(x, y, launchingPlane);
    }

    void TryDestroyImmediate(int x, int y, Gem exclude)
    {
        Gem g = GetGemAt(x, y);
        if (g == null) return;
        if (g == exclude) return;       // ne uništavaj avion
        if (g.isBeingDestroyed) return;

        g.isMatched = true;
        gems[g.x, g.y] = null;
        StartCoroutine(DestroyGemAnimated(g));
    }

    List<Gem> FindAllMatchesAndMarkIncludingSquares()
    {
        // ovo već resetuje i markira linije 3+
        FindAllMatchesAndMark();

        // dodaj squares
        MarkAll2x2Squares();

        // collect
        List<Gem> matched = new List<Gem>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (gems[x, y] != null && gems[x, y].isMatched)
                    matched.Add(gems[x, y]);

        return matched;
    }



    public Gem GetGemAt(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return null;
        return gems[x, y];
    }

    IEnumerator DestroyGemAnimated(Gem g)
    {
        if (g == null) yield break;

        int destroyedType = g.type;

        yield return StartCoroutine(g.AnimateDestroy());
        Destroy(g.gameObject);

        if (levelGoal != null)
            levelGoal.RegisterGemDestroyed(destroyedType);
    }
    // da ne aktiviramo isti specijal 20 puta u istom talasu
    private readonly HashSet<Gem> activatedThisWave = new HashSet<Gem>();
    private readonly Queue<Gem> activationQueue = new Queue<Gem>();
    // Disco koji je već aktiviran ovim potezom (swap), da ne radi random još jednom
    private readonly HashSet<Gem> discosActivatedBySwap = new HashSet<Gem>();
    // da ne lansiramo isti avion 2x u istom potezu
    private readonly HashSet<Gem> planesLaunchedThisAction = new HashSet<Gem>();

    // da avioni ne gađaju istu metu u istom potezu
    private readonly HashSet<Gem> reservedPlaneTargets = new HashSet<Gem>();


    void EnqueueSpecialIfNeeded(Gem g)
    {
        if (g == null) return;
        if (!g.IsSpecial) return;

        // ✅ Disco ulazi u chain samo ako je pogođen (nije potrošen swap-om)
        if (g.specialType == Gem.SpecialType.DiscoBall && discosActivatedBySwap.Contains(g))
            return;

        if (activatedThisWave.Contains(g)) return;

        activatedThisWave.Add(g);
        activationQueue.Enqueue(g);
    }
    void ResolveSpecialChainReactions()
    {
        while (activationQueue.Count > 0)
        {
            Gem s = activationQueue.Dequeue();
            if (s == null) continue;

            // specijal sam mora biti markiran da se uništi (ili da eksplodira)
            s.isMatched = true;

            if (s.specialType == Gem.SpecialType.RocketH)
            {
                for (int x = 0; x < width; x++)
                {
                    Gem g = gems[x, s.y];
                    if (g == null) continue;
                    g.isMatched = true;
                    EnqueueSpecialIfNeeded(g); // 🔥 ako na putu pogodi specijal -> aktiviraj ga
                }
            }
            else if (s.specialType == Gem.SpecialType.RocketV)
            {
                for (int y = 0; y < height; y++)
                {
                    Gem g = gems[s.x, y];
                    if (g == null) continue;
                    g.isMatched = true;
                    EnqueueSpecialIfNeeded(g);
                }
            }
            else if (s.specialType == Gem.SpecialType.Bomb)
            {
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        Gem g = GetGemAt(s.x + dx, s.y + dy);
                        if (g == null) continue;
                        g.isMatched = true;
                        EnqueueSpecialIfNeeded(g);
                    }
            }
            else if (s.specialType == Gem.SpecialType.PaperPlane)
            {
                // Najjednostavnije: ako je avion pogođen eksplozijom,
                // tretiraj ga kao "pogodio je metu" odmah (random target).
                // (To je homescapes-like: avion u lančanoj reakciji odmah leti.)
                Gem target = PickPlaneTarget(avoidSpecials: false);
                if (target != null)
                {
                    target.isMatched = true;
                    EnqueueSpecialIfNeeded(target); // ako pogodi specijal, i on se aktivira
                }

                // opcionalno: možeš pokrenuti i vizuelno FlyPlaneAndHit ovde,
                // ali gameplay chain može biti "instant" zbog jednostavnosti.
            }

            else if (s.specialType == Gem.SpecialType.DiscoBall)
            {
                // Disco pogođen => obriši jednu random boju (bez specijala)
                int typeToClear = Random.Range(0, gemSprites.Length);

                // da ne izabere "praznu" boju, pokušaj par puta da nađe postojeću
                for (int tries = 0; tries < 10; tries++)
                {
                    bool exists = false;
                    for (int x = 0; x < width && !exists; x++)
                        for (int y = 0; y < height && !exists; y++)
                            if (gems[x, y] != null && !gems[x, y].IsSpecial && gems[x, y].type == typeToClear)
                                exists = true;

                    if (exists) break;
                    typeToClear = Random.Range(0, gemSprites.Length);
                }

                MarkAllOfType(typeToClear, includeSpecials: false);

                // i svi pogođeni specijali od toga ulaze u lanac
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                    {
                        Gem g = gems[x, y];
                        if (g == null) continue;
                        if (!g.isMatched) continue;
                        EnqueueSpecialIfNeeded(g);
                    }
            }

        }
    }


}
