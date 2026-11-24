using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 가상 사출 훈련: 현장 조건을 정하고, 발동된 숙련자 조각을 적용할지 고른 뒤
// 100쇼트를 생산해 표준 매뉴얼과 결과를 비교한다. 적용한 조각은 원작성자의 활용 횟수로 기록된다.
// 오른쪽의 현장 에이전트 대화는 TrainingGame.Chat.cs에 있다.
public partial class TrainingGame : MonoBehaviour
{
    const int Shots = 100;
    const float CycleSeconds = 0.11f;
    const float IncentivePool = 1000000f; // 월 인센티브 재원(원), 예시
    const string WebUrl = "https://sonkkeut-ledger.vercel.app/#demo"; // 웹 서비스(인터뷰·도우미·원장)

    Env env = new Env { temp = 12f, humidity = 72f, age = 15f, tenure = 4f };
    List<Fragment> frags;
    Machine machine;

    readonly bool[] bgClosed = new bool[8]; // 뒤쪽 사출기가 직전 프레임에 닫혀 있었는지 (소리를 한 번만 내려고)
    bool running, hasResult, showLedger;
    int shotIndex, defects, stdDefects, seed = 7;
    string creditLine = "";
    readonly List<string> defectLog = new List<string>();

    // 화면 캡처 자동 실행용 (-shot <폴더>)
    string shotDir;

    Font font;
    Texture2D white;
    GUIStyle sTitle, sLabel, sSmall, sBtn, sBtnOn, sBig, sWrap, sHead;
    bool stylesReady;

    static readonly Color Ink = new Color(0.07f, 0.08f, 0.10f);
    static readonly Color Ink2 = new Color(0.32f, 0.33f, 0.36f);
    static readonly Color Blue = new Color(0.16f, 0.47f, 0.84f);   // 표준 매뉴얼
    static readonly Color Orange = new Color(0.92f, 0.41f, 0.20f); // 조각 적용
    static readonly Color Green = new Color(0.00f, 0.42f, 0.05f);

    void Awake()
    {
        Application.targetFrameRate = 60;
        // 브라우저에서 실행하면 웹페이지에 등록된 조각을 쓰고, 그 밖에는 기본 조각을 쓴다
        frags = WebStore.LoadFragments() ?? TacitData.CreateFragments();
        machine = MachineBuilder.Build();
        InitView(); // 1인칭 시점 준비 (TrainingGame.View.cs)
        white = Texture2D.whiteTexture;
        ChatStart();

        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-shot") shotDir = args[i + 1];
        if (shotDir != null) StartCoroutine(AutoCapture());
    }

    void Update()
    {
        TickCues();
        // 조각 상태등: 대기(회색) / 발동(주황) / 적용(초록)
        for (int i = 0; i < frags.Count && i < machine.lamps.Length; i++)
        {
            bool trig = TacitSim.Triggered(frags[i], env);
            Color c = !trig ? MachineBuilder.LampOff : frags[i].applied ? MachineBuilder.LampOn : MachineBuilder.LampWait;
            machine.lamps[i].material.color = c;
            MachineBuilder.SetGlow(machine.lamps[i], trig ? c * 1.5f : Color.black); // 켜진 표시등은 스스로 빛난다
        }
        // 경광등은 생산 중에 더 빠르게 깜빡이고, 평소에는 천천히 숨 쉬듯 밝아진다
        if (machine.beacon != null)
        {
            float rate = running ? 7f : 1.6f;
            float glow = 0.55f + 0.45f * Mathf.Sin(Time.time * rate);
            MachineBuilder.SetGlow(machine.beacon, (running ? MachineBuilder.LampWait : MachineBuilder.LampOn) * (0.4f + glow * 1.4f));
        }
        UpdateCamera(); // 1인칭 ↔ 전체 보기 (TrainingGame.View.cs)
        if (Input.GetKeyDown(KeyCode.M) && GUIUtility.keyboardControl == 0) FactoryAudio.Toggle(); // M: 소리 켜고 끄기
        if (!running)
        {
            Vector3 p = machine.movingPlaten.localPosition;
            p.x = Mathf.MoveTowards(p.x, Machine.OpenX, Time.deltaTime * 2f);
            machine.movingPlaten.localPosition = p;
        }
        AnimateWorkers();
        // 도어 제작 라인: 도어가 천장 컨베이어를 따라 흐르고, 작업 구간을 지나면 트림이 붙는다
        for (int i = 0; i < machine.hangingDoors.Count; i++)
        {
            Vector3 p = machine.hangingDoors[i].position;
            p.x += Time.deltaTime * 0.45f;
            if (p.x > Machine.DoorLineEnd) p.x = Machine.DoorLineStart;
            machine.hangingDoors[i].position = p;
            machine.doorTrims[i].enabled = p.x > Machine.TrimStationX;
        }
        // 뒤쪽 라인의 사출기들은 각자 다른 박자로 계속 돌아간다
        for (int i = 0; i < machine.backgroundPlatens.Count; i++)
        {
            float phase = Mathf.Repeat(Time.time / (3.2f + i * 0.7f) + i * 0.3f, 1f);
            float k = Mathf.Clamp01(Mathf.Sin(phase * Mathf.PI * 2f) * 1.6f + 0.5f);
            Vector3 p = machine.backgroundPlatens[i].localPosition;
            p.x = Mathf.Lerp(Machine.OpenX, Machine.ClosedX, k);
            machine.backgroundPlatens[i].localPosition = p;
            // 멀리 있는 사출기가 닫힐 때는 작게 쿵 소리가 난다
            bool closedNow = k > 0.97f;
            if (closedNow && !bgClosed[i]) FactoryAudio.PlayThud(0.10f);
            bgClosed[i] = closedNow;
        }
    }

    // 부드럽게 목표 자세로 다가간다 (톱니 모양으로 끊기지 않게)
    static void Pose(Transform t, Quaternion target, float rate = 12f)
    {
        t.localRotation = Quaternion.Slerp(t.localRotation, target, 1f - Mathf.Exp(-rate * Time.deltaTime));
    }

    // 가끔 일어나는 행동의 세기(0→1→0). period초마다 dur초 동안 일어나고, 작업자마다 시점이 다르다
    static float Gesture(WorkerRig w, float period, float dur, float offset)
    {
        float u = Mathf.Repeat(Time.time + w.phase * 3.1f + offset, period);
        return u >= dur ? 0f : Mathf.Sin(u / dur * Mathf.PI);
    }

    // 작업자 동작: 팔꿈치와 무릎이 굽고, 체중을 옮기고, 가끔 이마를 닦거나 허리를 펴고 주변을 둘러본다.
    // 조작반 누르기, 트림 조립, 통로 보행(끝에서 잠시 서서 기계를 본다)
    void AnimateWorkers()
    {
        foreach (WorkerRig w in machine.workers)
        {
            if (w.animator != null) { AnimateRealWorker(w); continue; } // 실제 사람 캐릭터는 동작 클립으로
            float t = Time.time + w.phase;
            // 숨쉬기: 상체가 천천히 부풀었다 가라앉는다
            float breath = Mathf.Sin(t * 1.6f);
            float chestW = w.chest.localScale.x;
            w.chest.localScale = new Vector3(chestW, 1f + breath * 0.018f, chestW);
            // 눈 깜빡임: 4초쯤마다 잠깐 감는다
            float blinkClose = Mathf.Repeat(Time.time * 0.23f + w.phase * 0.31f, 1f) < 0.035f ? 1f : 0f;
            foreach (Transform lid in w.eyes) // 눈꺼풀: 깜빡일 때만 눈 위로 내려온다
                lid.localScale = new Vector3(0.034f, Mathf.Lerp(0.0006f, 0.026f, blinkClose), 0.012f);

            // 가끔 하는 행동: 이마 닦기, 허리 펴기, 주변 둘러보기
            float wipe = w.mode == WorkerMode.Walk ? 0f : Gesture(w, 23f, 3.2f, 0f);
            float stretch = w.mode == WorkerMode.Walk ? 0f : Gesture(w, 31f, 3.6f, 9f);
            float look = Gesture(w, 17f, 3.4f, 4f);
            float side = (machine.workers.IndexOf(w) % 2 == 0) ? 1f : -1f;

            // 머리: 일할 땐 손을 보고, 가끔 고개를 돌려 주변을 본다
            float lookDown = w.mode == WorkerMode.Assemble ? 16f : (w.mode == WorkerMode.Operate && running ? 10f : 0f);
            float headYaw = Mathf.Sin(t * 0.37f) * (lookDown > 0f ? 7f : 14f) + look * 38f * side;
            float headPitch = Mathf.Sin(t * 0.6f) * 4f + lookDown * (1f - look) - wipe * 12f;
            Pose(w.head, Quaternion.Euler(headPitch, headYaw, look * 4f * side), 8f);

            Quaternion legIdle = Quaternion.identity;
            if (w.mode != WorkerMode.Walk)
            {
                // 가만히 서 있어도 체중을 번갈아 싣고 몸이 미세하게 흔들린다
                float shift = Mathf.Sin(t * 0.42f);
                w.root.localPosition = w.basePos + new Vector3(shift * 0.012f, 0f, 0f);
                w.root.localRotation = Quaternion.Euler(0f, w.baseYaw + Mathf.Sin(t * 0.31f) * 2.2f, 0f);
                float lean = w.mode == WorkerMode.Assemble ? 7f + Mathf.Sin(t * 2.2f) * 2.5f : (running ? 4f : 0f);
                Pose(w.chest, Quaternion.Euler(lean - stretch * 14f, look * 8f * side, shift * 1.2f), 8f);
                // 체중을 싣는 쪽 다리는 펴고 반대쪽은 무릎을 살짝 굽힌다
                Pose(w.hipL, Quaternion.Euler(shift * 3f, 0, 0), 6f);
                Pose(w.hipR, Quaternion.Euler(-shift * 3f, 0, 0), 6f);
                Pose(w.kneeL, Quaternion.Euler(7f + shift * 5f + stretch * 4f, 0, 0), 6f);
                Pose(w.kneeR, Quaternion.Euler(7f - shift * 5f + stretch * 4f, 0, 0), 6f);
            }

            switch (w.mode)
            {
                case WorkerMode.Operate:
                {
                    // 생산 중에는 오른손으로 조작반을 누르고 왼손은 허리 근처에 둔다. 쉴 땐 팔을 늘어뜨리고 화면을 본다
                    float rSh = running ? -60f + Mathf.Sin(t * 6f) * 5f : -16f + Mathf.Sin(t * 0.9f) * 3f;
                    float rEl = running ? -40f + Mathf.Sin(t * 6f + 1f) * 9f : -14f;
                    float lSh = running ? -16f : -10f + Mathf.Sin(t * 0.8f) * 3f;
                    float lEl = running ? -62f : -22f;
                    // 이마 닦기: 오른팔을 들어 올려 손등으로 이마를 닦는다
                    rSh = Mathf.Lerp(rSh, -150f, wipe);
                    rEl = Mathf.Lerp(rEl, -120f, wipe);
                    // 허리 펴기: 두 손을 허리에 짚고 뒤로 젖힌다
                    rSh = Mathf.Lerp(rSh, -8f, stretch); rEl = Mathf.Lerp(rEl, -88f, stretch);
                    lSh = Mathf.Lerp(lSh, -8f, stretch); lEl = Mathf.Lerp(lEl, -88f, stretch);
                    Pose(w.shoulderR, Quaternion.Euler(rSh, 0, stretch * 22f));
                    Pose(w.elbowR, Quaternion.Euler(rEl, 0, 0));
                    Pose(w.shoulderL, Quaternion.Euler(lSh, 0, -stretch * 22f));
                    Pose(w.elbowL, Quaternion.Euler(lEl, 0, 0));
                    break;
                }
                case WorkerMode.Assemble:
                {
                    // 양손을 번갈아 움직이며 트림을 끼운다: 어깨와 팔꿈치가 서로 다른 박자로 움직인다
                    float lSh = -64f + Mathf.Sin(t * 2.2f) * 14f;
                    float rSh = -64f + Mathf.Sin(t * 2.2f + Mathf.PI) * 14f;
                    float lEl = -46f + Mathf.Sin(t * 2.2f + 1.3f) * 22f;
                    float rEl = -46f + Mathf.Sin(t * 2.2f + Mathf.PI + 1.3f) * 22f;
                    rSh = Mathf.Lerp(rSh, -150f, wipe); rEl = Mathf.Lerp(rEl, -120f, wipe);
                    lSh = Mathf.Lerp(lSh, -8f, stretch); lEl = Mathf.Lerp(lEl, -88f, stretch);
                    rSh = Mathf.Lerp(rSh, -8f, stretch); rEl = Mathf.Lerp(rEl, -88f, stretch);
                    Pose(w.shoulderL, Quaternion.Euler(lSh, 0, -stretch * 22f));
                    Pose(w.elbowL, Quaternion.Euler(lEl, 0, 0));
                    Pose(w.shoulderR, Quaternion.Euler(rSh, 0, stretch * 22f));
                    Pose(w.elbowR, Quaternion.Euler(rEl, 0, 0));
                    break;
                }
                case WorkerMode.Walk:
                {
                    bool paused = Time.time < w.pauseUntil;
                    Vector3 p = w.root.position;
                    if (!paused)
                    {
                        p.x += w.walkDir * 0.8f * Time.deltaTime;
                        if (p.x > w.walkMaxX || p.x < w.walkMinX)
                        {
                            w.walkDir = p.x > w.walkMaxX ? -1f : 1f;
                            // 통로 끝에서 잠시 멈춰 서서 기계 쪽을 본다
                            w.pauseUntil = Time.time + 1.8f + 1.6f * Mathf.Abs(Mathf.Sin(w.phase * 7f + Time.time));
                        }
                    }
                    float gait = paused ? 0f : 1f;
                    float cyc = t * 5f;
                    p.y = paused ? 0f : Mathf.Abs(Mathf.Sin(cyc)) * 0.03f; // 걸을 때 몸이 위아래로 출렁인다
                    w.root.position = p;
                    // 방향을 바꿀 때 휙 돌지 않고 부드럽게 돈다. 멈춰 있을 땐 기계(앞쪽 -z)를 본다
                    Quaternion face = paused ? Quaternion.Euler(0, 180f, 0) : Quaternion.Euler(0, w.walkDir > 0 ? 90f : -90f, 0);
                    w.root.rotation = Quaternion.Slerp(w.root.rotation, face, Time.deltaTime * 4f);
                    Pose(w.chest, Quaternion.Euler(3f * gait, Mathf.Sin(cyc) * 5f * gait, 0f), 8f);
                    float swing = Mathf.Sin(cyc) * 26f * gait;
                    Pose(w.hipL, Quaternion.Euler(swing, 0, 0), 14f);
                    Pose(w.hipR, Quaternion.Euler(-swing, 0, 0), 14f);
                    // 다리가 앞으로 나가는 동안 무릎이 접힌다
                    float kl = paused ? 5f : 8f + 30f * Mathf.Max(0f, -Mathf.Cos(cyc));
                    float kr = paused ? 5f : 8f + 30f * Mathf.Max(0f, Mathf.Cos(cyc));
                    Pose(w.kneeL, Quaternion.Euler(kl, 0, 0), 14f);
                    Pose(w.kneeR, Quaternion.Euler(kr, 0, 0), 14f);
                    // 팔은 다리와 반대로 흔들고, 앞으로 갈 때 팔꿈치가 더 굽는다. 멈춰 서면 한 손을 턱에 댄다
                    if (paused)
                    {
                        Pose(w.shoulderR, Quaternion.Euler(-58f, 0, 6f), 8f);
                        Pose(w.elbowR, Quaternion.Euler(-112f, 0, 0), 8f);
                        Pose(w.shoulderL, Quaternion.Euler(-12f, 0, -4f), 8f);
                        Pose(w.elbowL, Quaternion.Euler(-30f, 0, 0), 8f);
                    }
                    else
                    {
                        Pose(w.shoulderL, Quaternion.Euler(-swing * 0.7f, 0, 0), 14f);
                        Pose(w.shoulderR, Quaternion.Euler(swing * 0.7f, 0, 0), 14f);
                        Pose(w.elbowL, Quaternion.Euler(-18f - 24f * Mathf.Max(0f, swing / 26f), 0, 0), 14f);
                        Pose(w.elbowR, Quaternion.Euler(-18f - 24f * Mathf.Max(0f, -swing / 26f), 0, 0), 14f);
                    }
                    break;
                }
            }
        }
    }

    // ---------- 생산 ----------

    IEnumerator Run()
    {
        running = true; hasResult = false; showLedger = false;
        shotIndex = 0; defects = 0; stdDefects = 0; defectLog.Clear();
        foreach (Transform t in machine.partsRoot) Destroy(t.gameObject);
        ChatRunStarted();

        // 같은 난수로 두 경우를 비교한다: 조각을 적용해 줄어든 불량만 차이로 나타난다
        var rng = new System.Random(seed++);
        SimResult std = TacitSim.Evaluate(env, frags, false);
        SimResult cur = TacitSim.Evaluate(env, frags, true);

        for (int i = 0; i < Shots; i++)
        {
            float t = 0f;
            while (t < CycleSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(t / CycleSeconds) * Mathf.PI);
                Vector3 p = machine.movingPlaten.localPosition;
                p.x = Mathf.Lerp(Machine.OpenX, Machine.ClosedX, k);
                machine.movingPlaten.localPosition = p;
                yield return null;
            }
            FactoryAudio.PlayThud(0.28f); // 금형이 닫히는 소리
            float u = (float)rng.NextDouble();
            float u2 = (float)rng.NextDouble();
            if (u < std.defect / 100f) stdDefects++;
            bool bad = u < cur.defect / 100f;
            if (bad)
            {
                defects++;
                if (defects == 1) Cue("first_defect");
                defectLog.Add((i + 1) + "번째 쇼트: " + TacitSim.PickDefectType(cur, u2));
            }
            StartCoroutine(Fly(MachineBuilder.MakePart(machine, bad), machine.Slot(i)));
            shotIndex = i + 1;
            if (shotIndex == Shots / 2) Cue(defects <= 3 ? "mid_ok" : "mid_bad");
        }

        // 활용 기록: 발동된 조각을 작업자가 적용하고 생산을 마치면 1회
        var credits = new List<string>();
        foreach (Fragment f in frags)
        {
            if (!f.applied || !TacitSim.Triggered(f, env)) continue;
            f.uses++;
            credits.Add(f.id + "(" + f.author + ")");
        }
        creditLine = credits.Count > 0
            ? "활용 기록 +1: " + string.Join(", ", credits.ToArray())
            : "따른 조각이 없어 활용 기록이 없습니다.";
        if (credits.Count > 0)
        {
            WebStore.SaveUses(frags);
            if (WebStore.Linked) creditLine += "  (웹 보상 원장에 반영됨)";
        }
        running = false; hasResult = true;
        ChatRunFinished(credits);
    }

    IEnumerator Fly(GameObject part, Vector3 target)
    {
        Vector3 from = part.transform.position;
        float t = 0f;
        while (t < 1f && part != null)
        {
            t += Time.deltaTime / 0.3f;
            Vector3 p = Vector3.Lerp(from, target, t);
            p.y += Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.5f;
            part.transform.position = p;
            yield return null;
        }
        if (part != null) part.transform.position = target;
    }

    void Preset(float temp, float humidity)
    {
        env.temp = temp; env.humidity = humidity;
        hasResult = false;
    }

    // ---------- 화면 ----------

    void InitStyles()
    {
        // 프로젝트에 넣은 글꼴(나눔바른고딕)을 쓴다. 브라우저에서는 컴퓨터 글꼴을 쓸 수 없기 때문이다
        font = Resources.Load<Font>("AppFont");
        if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 20);
        GUI.skin.font = font;
        sLabel = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = false };
        sLabel.normal.textColor = Ink;
        sSmall = new GUIStyle(sLabel) { fontSize = 17 };
        sSmall.normal.textColor = Ink2;
        sWrap = new GUIStyle(sLabel) { fontSize = 18, wordWrap = true };
        sTitle = new GUIStyle(sLabel) { fontSize = 26, fontStyle = FontStyle.Bold };
        sHead = new GUIStyle(sLabel) { fontSize = 20, fontStyle = FontStyle.Bold };
        sBig = new GUIStyle(sLabel) { fontSize = 40, fontStyle = FontStyle.Bold };
        sBtn = MakeButton(new Color(0.10f, 0.11f, 0.13f), new Color(0.25f, 0.27f, 0.31f), Color.white);
        sBtnOn = MakeButton(Green, new Color(0.00f, 0.52f, 0.08f), Color.white);
        stylesReady = true;
    }

    GUIStyle MakeButton(Color normal, Color hover, Color text)
    {
        var s = new GUIStyle(GUI.skin.button) { fontSize = 19, alignment = TextAnchor.MiddleCenter, font = font };
        s.normal.background = Solid(normal); s.hover.background = Solid(hover); s.active.background = Solid(hover);
        s.normal.textColor = text; s.hover.textColor = text; s.active.textColor = text;
        s.border = new RectOffset(0, 0, 0, 0);
        return s;
    }

    static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c); t.Apply();
        return t;
    }

    Texture2D vignette;

    static Texture2D MakeVignette()
    {
        const int n = 128;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x / (n - 1f) - 0.5f) * 2f, dy = (y / (n - 1f) - 0.5f) * 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                float a = Mathf.SmoothStep(0.45f, 1.0f, d) * 0.55f;
                t.SetPixel(x, y, new Color(0f, 0f, 0.02f, a));
            }
        t.wrapMode = TextureWrapMode.Clamp;
        t.Apply();
        return t;
    }

    void Fill(Rect r, Color c)
    {
        Color old = GUI.color; GUI.color = c;
        GUI.DrawTexture(r, white);
        GUI.color = old;
    }

    void Panel(Rect r) { Fill(r, new Color(0.99f, 0.99f, 0.98f, 0.95f)); }

    void OnGUI()
    {
        if (!stylesReady) InitStyles();
        GUI.skin.font = font;

        // 1080p 기준으로 그리고 화면 크기에 맞춰 확대·축소
        float scale = Screen.height / 1080f;
        float W = Screen.width / scale;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

        // 화면 가장자리를 살짝 어둡게 (카메라 렌즈 느낌)
        if (!PostFx.Active) // 후처리가 켜져 있으면 셰이더가 비네팅을 그린다
        {
            if (vignette == null) vignette = MakeVignette();
            GUI.DrawTexture(new Rect(0, 0, W, 1080), vignette, ScaleMode.StretchToFill, true);
        }

        if (showDoc)
        {
            // 자료를 열면 자료 화면만 보인다 (뒤는 어둡게, 다른 패널과 입력칸은 숨김)
            HideWebInput();
            FillA(new Rect(0, 0, W, 1080), new Color(0.04f, 0.05f, 0.07f, 0.78f));
            DrawDoc(new Rect(W / 2f - 480f, 110f, 960f, 760f));
            if (GUI.Button(new Rect(W / 2f - 480f, 24f, 220f, 56f), "← 돌아가기", sBtnOn)) showDoc = false;
            return;
        }
        DrawWorldLabels(scale);
        DrawSubtitle(W);
        DrawConditions(new Rect(24, 24, 440, 700));
        DrawChat(new Rect(W - 64 - 540, 60, 540, 910), scale);
        DrawProduction(new Rect(488, 1080 - 24 - 250, W - 488 - 704, 250));
        DrawHeldHands(heldRect);
        DrawViewToggle(W);
        if (showLedger) DrawLedger(new Rect(Mathf.Max(480f, Mathf.Min(W / 2f - 430f, W - 604f - 860f - 16f)), 150, 860, 520)); // 태블릿을 가리지 않게 왼쪽으로
    }

    // 표지판 글자를 3D 위치에 맞춰 화면에 그린다 (패널보다 먼저 그려서 패널에 가려지게 한다)
    void DrawWorldLabels(float scale)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        foreach (WorldLabel l in machine.labels)
        {
            if (l.isSelf && blend > 0.5f) continue; // 1인칭에서는 내 이름표가 없다
            Vector3 c = cam.WorldToScreenPoint(l.position);
            if (c.z <= 0f) continue;
            float top = cam.WorldToScreenPoint(l.position + Vector3.up * l.height * 0.5f).y;
            float bottom = cam.WorldToScreenPoint(l.position - Vector3.up * l.height * 0.5f).y;
            float px = Mathf.Abs(top - bottom) / scale;
            var style = new GUIStyle(sLabel)
            {
                fontSize = Mathf.Max(8, Mathf.RoundToInt(px * 0.8f)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = l.color;
            float x = c.x / scale, y = (Screen.height - c.y) / scale;
            GUI.Label(new Rect(x - 400, y - px, 800, px * 2f), l.text, style);
        }
    }

    void DrawConditions(Rect r)
    {
        Panel(r);
        float x = r.x + 20, y = r.y + 16, w = r.width - 40;
        GUI.Label(new Rect(x, y, w, 36), TacitData.PersonaName + " 씨의 야간 교대", sTitle); y += 40;
        GUI.Label(new Rect(x, y, w, 30), "입사 4주차 · 도어 트림 사출 · " + TacitData.MentorName + " 부재", sSmall); y += 40;

        GUI.enabled = !running;
        float bw = (w - 16) / 3f;
        if (GUI.Button(new Rect(x, y, bw, 40), "장마철 야간", sBtn)) ChatPreset("장마철 야간", 24f, 90f);
        if (GUI.Button(new Rect(x + bw + 8, y, bw, 40), "한겨울 새벽", sBtn)) ChatPreset("한겨울 새벽", -3f, 35f);
        if (GUI.Button(new Rect(x + 2 * (bw + 8), y, bw, 40), "열대야", sBtn)) ChatPreset("열대야", 31f, 72f);
        y += 60;

        env.temp = Slider(x, ref y, w, "외기온도", env.temp, -10f, 40f, "℃");
        env.humidity = Slider(x, ref y, w, "습도", env.humidity, 20f, 95f, "%");
        env.age = Slider(x, ref y, w, "설비 사용연수", env.age, 0f, 35f, "년");
        env.tenure = Slider(x, ref y, w, "내 근속", env.tenure, 0f, 52f, "주");
        GUI.enabled = true;

        // 예상 불량률 비교
        SimResult std = TacitSim.Evaluate(env, frags, false);
        SimResult cur = TacitSim.Evaluate(env, frags, true);
        y += 6;
        GUI.Label(new Rect(x, y, w, 34), "예상 불량률", sHead); y += 38;
        float max = Mathf.Max(12f, std.defect);
        RateBar(x, ref y, w, "작업표준서만 보면", std.defect, max, Blue);
        RateBar(x, ref y, w, "숙련자 조각을 따르면", cur.defect, max, Orange);
        GUI.Label(new Rect(x, y, w, 24), "월 다운타임 " + std.down.ToString("0.0") + "h → " + cur.down.ToString("0.0") + "h", sSmall);
        y += 30;
        GUI.Label(new Rect(x, y, w, 24), "수치는 예시 가정 모델입니다", sSmall);
    }

    float Slider(float x, ref float y, float w, string label, float v, float min, float max, string unit)
    {
        GUI.Label(new Rect(x, y, 220, 34), label, sLabel);
        var right = new GUIStyle(sHead) { alignment = TextAnchor.UpperRight };
        GUI.Label(new Rect(x + w - 120, y, 120, 34), Mathf.Round(v) + unit, right);
        y += 34;
        float nv = Mathf.Round(GUI.HorizontalSlider(new Rect(x, y, w, 24), v, min, max));
        if (nv != v) hasResult = false;
        y += 36;
        return nv;
    }

    void RateBar(float x, ref float y, float w, string label, float value, float max, Color c)
    {
        GUI.Label(new Rect(x, y, 200, 30), label, sSmall);
        var right = new GUIStyle(sHead) { alignment = TextAnchor.UpperRight };
        GUI.Label(new Rect(x + w - 100, y - 2, 100, 34), value.ToString("0.0") + "%", right);
        y += 32;
        Fill(new Rect(x, y, w, 12), new Color(0, 0, 0, 0.08f));
        Fill(new Rect(x, y, w * Mathf.Clamp01(value / max), 12), c);
        y += 26;
    }

    // 조각 카드 한 장. 에이전트 대화 안에서, 지금 발동된 조각마다 한 장씩 그린다
    // 카드 내용: 증상 → 확인 순서 → 원인, 그리고 남긴 사람과 활용 횟수
    const float CardHeight = 186f;
    void DrawCard(Rect card, Fragment f)
    {
        float y = card.y;
        bool trig = TacitSim.Triggered(f, env);
        Fill(card, Color.white);
        Fill(new Rect(card.x, card.y, 6, card.height), !trig ? MachineBuilder.LampOff : f.applied ? MachineBuilder.LampOn : MachineBuilder.LampWait);

        float cx = card.x + 18, cw = card.width - 32;
        string state = !trig ? "  ○ 지금 조건 밖" : f.applied ? "  ● 따르는 중" : "  ● 해당";
        GUI.Label(new Rect(cx, y + 4, 320, 34), f.id + state, sHead);
        var usesStyle = new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight };
        GUI.Label(new Rect(card.xMax - 174, y + 7, 160, 30), "활용 " + f.uses + "회", usesStyle);
        GUI.Label(new Rect(cx, y + 36, cw, 28), "증상: " + f.defectType + " · 주로 " + TacitData.ConditionText(f), sSmall);
        GUI.Label(new Rect(cx, y + 62, cw, 54), f.action, sWrap);
        GUI.Label(new Rect(cx, y + 116, cw, 28), "원인: " + (string.IsNullOrEmpty(f.cause) ? "기록 없음" : f.cause), sSmall);
        GUI.Label(new Rect(cx, y + 148, cw - 130, 28), f.author + " " + TacitData.ExpertLabel(f.author), sSmall);

        bool was = GUI.enabled;
        GUI.enabled = was && !running && trig;
        if (GUI.Button(new Rect(card.xMax - 126, y + 140, 112, 36), f.applied ? "따르는 중" : "따르기", f.applied ? sBtnOn : sBtn))
        {
            f.applied = !f.applied;
            hasResult = false;
            if (f.applied) Cue("follow");
        }
        GUI.enabled = was;
    }

    void DrawProduction(Rect r)
    {
        Panel(r);
        float x = r.x + 20, y = r.y + 14, w = r.width - 40;

        if (running)
        {
            GUI.Label(new Rect(x, y, w, 36), "생산 중  " + shotIndex + " / " + Shots + " 쇼트", sTitle);
            GUI.Label(new Rect(x, y + 46, w, 50), "불량 " + defects + "개", sBig);
            Fill(new Rect(x, y + 110, w, 12), new Color(0, 0, 0, 0.08f));
            Fill(new Rect(x, y + 110, w * shotIndex / Shots, 12), Ink);
            if (defectLog.Count > 0)
                GUI.Label(new Rect(x, y + 136, w, 26), defectLog[defectLog.Count - 1], sSmall);
            return;
        }

        if (hasResult)
        {
            GUI.Label(new Rect(x, y, w, 36), "생산 결과", sTitle);
            GUI.Label(new Rect(x, y + 42, 420, 50), "불량 " + defects + " / " + Shots + "개", sBig);
            int saved = stdDefects - defects;
            string cmp = "작업표준서만 봤다면 " + stdDefects + "개";
            if (saved > 0) cmp += "  →  숙련자 조각으로 " + saved + "개를 막았습니다";
            GUI.Label(new Rect(x, y + 98, w, 34), cmp, sLabel);
            GUI.Label(new Rect(x, y + 132, w, 30), creditLine, sSmall);
            if (GUI.Button(new Rect(x, y + 172, 200, 46), "다시 생산", sBtn)) StartCoroutine(Run());
            if (GUI.Button(new Rect(x + 212, y + 172, 220, 46), "보상 원장 보기", sBtnOn)) showLedger = true;
        }
        else
        {
            GUI.Label(new Rect(x, y, w, 36), "도어 트림 부품 " + Shots + "쇼트를 생산해 보세요", sTitle);
            GUI.Label(new Rect(x, y + 44, w, 60),
                "조각을 따르지 않으면 작업표준서만 보고 생산합니다.\n같은 조건에서 따르기 전후를 비교해 보세요.", sWrap);
            if (GUI.Button(new Rect(x, y + 172, 260, 46), Shots + "쇼트 생산 시작", sBtn)) StartCoroutine(Run());
        }
    }

    void DrawLedger(Rect r)
    {
        Fill(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), new Color(0, 0, 0, 0.35f));
        Fill(r, new Color(1f, 1f, 1f, 1f));
        float x = r.x + 28, y = r.y + 22, w = r.width - 56;
        GUI.Label(new Rect(x, y, w, 36), "숙련자 기여도 보상 원장", sTitle); y += 42;
        GUI.Label(new Rect(x, y, w, 26),
            "기여 점수 = Σ 활용 횟수 × (불량 완화율 + 다운타임 완화율) · 월 재원 " + IncentivePool.ToString("N0") + "원 (예시)", sSmall);
        y += 44;

        float[] cols = { 0, 330, 440, 550, 660 };
        string[] heads = { "원작성 숙련자", "조각", "활용", "점수", "배분액" };
        for (int i = 0; i < heads.Length; i++) GUI.Label(new Rect(x + cols[i], y, 200, 34), heads[i], sHead);
        y += 40;
        Fill(new Rect(x, y - 4, w, 2), new Color(0, 0, 0, 0.15f));

        var count = new Dictionary<string, int>();
        var uses = new Dictionary<string, int>();
        var score = new Dictionary<string, float>();
        float total = 0f;
        foreach (Fragment f in frags)
        {
            if (!count.ContainsKey(f.author)) { count[f.author] = 0; uses[f.author] = 0; score[f.author] = 0f; }
            count[f.author]++;
            uses[f.author] += f.uses;
            float s = f.uses * (f.mDefect + f.mDown);
            score[f.author] += s; total += s;
        }
        foreach (string a in count.Keys)
        {
            float share = total > 0 ? score[a] / total : 0f;
            GUI.Label(new Rect(x + cols[0], y, 320, 34), a + "  " + TacitData.ExpertLabel(a), sLabel);
            GUI.Label(new Rect(x + cols[1], y, 100, 34), count[a] + "건", sLabel);
            GUI.Label(new Rect(x + cols[2], y, 100, 34), uses[a] + "회", sLabel);
            GUI.Label(new Rect(x + cols[3], y, 100, 34), score[a].ToString("0.0"), sLabel);
            GUI.Label(new Rect(x + cols[4], y, 160, 34), (share * IncentivePool).ToString("N0") + "원", sHead);
            y += 38;
            Fill(new Rect(x, y, w, 12), new Color(0, 0, 0, 0.08f));
            Fill(new Rect(x, y, w * share, 12), Blue);
            y += 30;
        }
        y += 10;
        GUI.Label(new Rect(x, y, w, 52),
            "조각이 현장에서 쓰일 때마다 원작성자의 활용 횟수가 올라가고, 그만큼 보상 지분이 커집니다.", sWrap);
        // 웹 서비스의 전체 원장(숙련자 인터뷰·신입 도우미 포함)을 브라우저로 연다
        if (GUI.Button(new Rect(r.x + 28, r.yMax - 66, 280, 44), "웹에서 전체 원장 보기", sBtnOn)) Application.OpenURL(WebUrl);
        if (GUI.Button(new Rect(r.xMax - 168, r.yMax - 66, 140, 44), "닫기", sBtn)) showLedger = false;
    }


    // ---------- 검증용 자동 캡처 ----------

    IEnumerator AutoCapture()
    {
        System.IO.Directory.CreateDirectory(shotDir);
        yield return new WaitForSeconds(1.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shotDir, "1_start.png"));
        yield return new WaitForSeconds(0.5f);
        if (captureHammer)
            for (int hi = 0; hi < 8; hi++)
            {
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shotDir, "h" + hi + ".png"));
                yield return new WaitForSeconds(0.2f);
            }

        // 민준 씨의 03:16: 저온·고습의 밤에 본 대로 적는다
        Preset(4f, 88f);
        yield return null;
        Submit("하얀 물결 같은 게 생겨요");
        yield return new WaitForSeconds(0.8f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shotDir, "5_symptom.png"));
        yield return new WaitForSeconds(0.5f);
        ShowCards();
        yield return null;
        foreach (Fragment f in frags) f.applied = TacitSim.Triggered(f, env);
        StartCoroutine(Run());
        yield return new WaitForSeconds(4f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shotDir, "2_running.png"));
        while (running) yield return null;
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shotDir, "3_result.png"));
        yield return new WaitForSeconds(0.5f);
        showLedger = true;
        yield return new WaitForSeconds(0.3f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shotDir, "4_ledger.png"));
        yield return new WaitForSeconds(0.8f);
        Application.Quit();
    }
}
