using UnityEngine;
using UnityEngine.Rendering;

// 시점: 기본은 김민준 씨의 1인칭(조작반 앞에서 금형을 바라봄), V키나 버튼으로 공장 전체 보기로 바꾼다.
// 1인칭에서는 빈 화면을 드래그해 고개를 돌리고, 화면 아래에 장갑 낀 양손이 보인다.
public partial class TrainingGame
{
    const bool ShotCamera = false;    // 대사할 때 말하는 사람 앞으로 카메라를 옮길지
    bool firstPerson = true;
    float blend = 1f;                 // 0 = 전체 보기, 1 = 1인칭 (부드럽게 오간다)
    float lookYaw, lookPitch;         // 마우스로 돌린 만큼 (처음 바라보는 방향 기준)
    float baseYaw, basePitch;         // 처음 바라보는 방향: 조작반과 금형 사이
    bool dragging, bodyHidden;
    Vector3 lastMouse;
    Transform hands, handL, handR;
    GUIStyle sHint;

    void InitView()
    {
        if (machine.self == null || machine.cam == null) return;
        // 눈 위치에서 조작반과 금형 사이를 바라보는 방향을 처음 방향으로 삼는다
        Vector3 eye = EyePosition();
        Quaternion look = Quaternion.LookRotation(new Vector3(-0.9f, 1.45f, 0.0f) - eye);
        Vector3 e = look.eulerAngles;
        baseYaw = e.y;
        basePitch = e.x > 180f ? e.x - 360f : e.x;
        machine.cam.nearClipPlane = 0.05f;
        // 화면 캡처 확인용: -overview 를 주면 전체 보기로 시작한다
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-overview") { firstPerson = false; blend = 0f; }
            if (args[i] == "-doc") showDoc = true;                  // 캡처 확인용: 자료 패널을 연 채로 시작
            if (args[i] == "-docpage" && i + 1 < args.Length) int.TryParse(args[i + 1], out docPage);
            if (args[i] == "-say") captureSay = true;                // 캡처 확인용: 조립 작업자가 바로 한 마디 한다
            if (args[i] == "-lookyaw" && i + 1 < args.Length && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)) lookYaw = Mathf.Clamp(y, -85f, 85f);
            if (args[i] == "-camto" && i + 1 < args.Length) { machine.camBase = ParseVec(args[i + 1]); firstPerson = false; blend = 0f; }
            if (args[i] == "-camlook" && i + 1 < args.Length) machine.camTarget = ParseVec(args[i + 1]);
            if (args[i] == "-lookpitch" && i + 1 < args.Length && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float pch)) lookPitch = Mathf.Clamp(pch, -40f, 35f);
        }
    }

    // 캡처 확인용: "x,y,z" 문자열을 좌표로
    static Vector3 ParseVec(string t)
    {
        string[] p = t.Split(',');
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        return new Vector3(float.Parse(p[0], ci), float.Parse(p[1], ci), float.Parse(p[2], ci));
    }

    // 김민준의 눈: 서 있는 자리에서 조금 뒤 (조작반 바로 앞이면 버튼만 가득 차서, 조작반과 금형이 함께 보이게 물러선다)
    Vector3 EyePosition()
    {
        WorkerRig s = machine.self;
        if (s.eyeRenderer != null) return s.eyeRenderer.bounds.center - s.root.forward * 0.60f; // 실제 캐릭터: 눈 위치에서 뒤로
        return s.head.position - s.root.forward * 0.60f + Vector3.up * 0.02f;
    }

    void UpdateCamera()
    {
        if (machine.cam == null || machine.self == null) return;
        Transform ct = machine.cam.transform;
        if (Input.GetKeyDown(KeyCode.V) && GUIUtility.keyboardControl == 0) firstPerson = !firstPerson;
        blend = Mathf.MoveTowards(blend, firstPerson ? 1f : 0f, Time.deltaTime * 1.4f);
        float k = Mathf.SmoothStep(0f, 1f, blend);
        HandleLook();

        // 전체 보기 자세: 고정 카메라가 아주 조금씩 흔들린다
        float st = Time.time;
        Vector3 ovPos = machine.camBase + new Vector3(Mathf.Sin(st * 0.21f) * 0.07f + Mathf.Sin(st * 0.53f) * 0.015f, Mathf.Sin(st * 0.17f) * 0.035f, Mathf.Sin(st * 0.13f) * 0.05f);
        Vector3 ovTarget = machine.camTarget + new Vector3(Mathf.Sin(st * 0.19f) * 0.05f, Mathf.Sin(st * 0.27f) * 0.03f, 0f);
        Quaternion ovRot = Quaternion.LookRotation(ovTarget - ovPos);

        // 1인칭 자세: 숨 쉴 때 시선이 미세하게 오르내리고, 걸음마다 아주 조금 흔들린다
        Vector3 eye = EyePosition() + Vector3.up * (Mathf.Sin(st * 1.6f) * 0.004f);
        Quaternion povRot = Quaternion.Euler(basePitch + lookPitch + Mathf.Sin(st * 1.6f) * 0.25f, baseYaw + lookYaw + Mathf.Sin(st * 0.37f) * 0.35f, 0f);

        ct.position = Vector3.Lerp(ovPos, eye, k);
        ct.rotation = Quaternion.Slerp(ovRot, povRot, k);
        machine.cam.fieldOfView = Mathf.Lerp(42f, 68f, k);

        // 팀원이 말할 때는 카메라가 그 사람 앞으로 다가갔다가, 말이 끝나면 내 자리로 돌아온다
        // 말하는 사람 앞으로 카메라가 날아가면 벽이나 몸에 파묻혀 화면이 잘리고 끊겨 보여서 끈다. 카메라는 늘 내 눈높이에 있다
        bool shotOn = ShotCamera && firstPerson && shotWorker != null && Time.time < cueUntil - 0.3f;
        shotK = Mathf.MoveTowards(shotK, shotOn ? 1f : 0f, Time.deltaTime * 0.9f);
        float sk = Mathf.SmoothStep(0f, 1f, shotK);
        if (sk > 0.001f && shotWorker != null)
        {
            Vector3 sp; Quaternion sr;
            ShotPose(shotWorker, out sp, out sr);
            ct.position = Vector3.Lerp(ct.position, sp, sk);
            ct.rotation = Quaternion.Slerp(ct.rotation, sr, sk);
            machine.cam.fieldOfView = Mathf.Lerp(machine.cam.fieldOfView, 52f, sk);
        }
        k *= 1f - sk; // 카메라가 나를 떠나 있는 동안은 내 손이 보이지 않는다

        // 내 몸은 보이지 않게 (그림자만 바닥에 남긴다)
        bool hide = k > 0.5f;
        if (hide != bodyHidden)
        {
            bodyHidden = hide;
            foreach (Renderer r in machine.self.root.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        UpdateHands(k);
        UpdateFrontWall();
    }

    // 전체 보기 카메라는 앞벽(z=-9.2) 바깥에 있어서, 벽이 시야를 가린다. 전체 보기일 때는 앞벽을 그림자만 남기고 숨긴다
    Renderer[] frontWallRenderers;
    bool frontWallHidden;
    void UpdateFrontWall()
    {
        if (frontWallRenderers == null)
        {
            var list = new System.Collections.Generic.List<Renderer>();
            foreach (string n in new[] { "FrontWall", "FrontWallBand", "FrontWindow0", "FrontWindow1", "FrontWindow2", "FrontWindow3", "FrontWindow4" })
            {
                GameObject g = GameObject.Find(n);
                if (g == null) continue;
                var r = g.GetComponent<Renderer>();
                if (r != null) list.Add(r);
            }
            frontWallRenderers = list.ToArray();
        }
        bool hide = blend < 0.6f;
        if (hide == frontWallHidden) return;
        frontWallHidden = hide;
        foreach (Renderer r in frontWallRenderers)
            if (r != null) r.shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
    }


    // 화면 빈 곳을 드래그하면 고개가 돌아간다 (패널 위에서는 돌아가지 않는다)
    void HandleLook()
    {
        if (!firstPerson) { dragging = false; return; }
        float scale = Screen.height / 1080f;
        if (Input.GetMouseButtonDown(0))
        {
            dragging = FreeArea(Input.mousePosition, scale);
            lastMouse = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(0)) dragging = false;
        if (dragging && Input.GetMouseButton(0))
        {
            Vector3 d = Input.mousePosition - lastMouse;
            lastMouse = Input.mousePosition;
            lookYaw = Mathf.Clamp(lookYaw + d.x * 0.18f, -85f, 85f);
            lookPitch = Mathf.Clamp(lookPitch - d.y * 0.18f, -40f, 35f);
        }
    }

    bool FreeArea(Vector3 mouse, float scale)
    {
        float W = Screen.width / scale;
        var p = new Vector2(mouse.x / scale, (Screen.height - mouse.y) / scale);
        if (OverPanels(p, W)) return false;                                          // 조건·안내·생산·태블릿 (TrainingGame.Story.cs)
        float cx = 488 + (W - 1076) / 2f;
        if (new Rect(cx - 150, 20, 440, 96).Contains(p)) return false;               // 시점·자료 버튼
        if (showDoc && new Rect(W / 2f - 480f, 110f, 960f, 720f).Contains(p)) return false; // 자료 패널
        if (showLedger && new Rect(Mathf.Max(480f, Mathf.Min(W / 2f - 430f, W - 604f - 860f - 16f)), 150, 860, 520).Contains(p)) return false;
        return true;
    }

    // ---------- 양손 ----------

    void UpdateHands(float k)
    {
        bool show = k > 0.6f;
        if (hands == null)
        {
            if (!show) return;
            BuildHands();
        }
        if (hands.gameObject.activeSelf != show) hands.gameObject.SetActive(show);
        if (!show) return;

        float t = Time.time;
        // 왼손은 가만히, 오른손은 생산 중에 조작반 버튼을 누르는 동작
        Vector3 l = new Vector3(-0.25f, -0.23f, 0.46f) + new Vector3(Mathf.Sin(t * 0.9f) * 0.004f, Mathf.Sin(t * 1.3f) * 0.004f, 0f);
        Vector3 r = new Vector3(0.25f, -0.23f, 0.46f) + new Vector3(Mathf.Sin(t * 0.8f + 1f) * 0.004f, Mathf.Sin(t * 1.1f) * 0.004f, 0f);
        float tap = running ? Mathf.Max(0f, Mathf.Sin(t * 12f)) : 0f;
        if (running)
        {
            r += new Vector3(-0.05f, 0.08f, 0.10f - tap * 0.07f);
            handR.localRotation = Quaternion.Euler(-28f - tap * 14f, -6f, 0f);
        }
        else handR.localRotation = Quaternion.Euler(-22f, -6f, 0f);
        handL.localRotation = Quaternion.Euler(-22f, 6f, 0f);
        handL.localPosition = l;
        handR.localPosition = r;
    }

    void BuildHands()
    {
        hands = new GameObject("ViewHands").transform;
        hands.SetParent(machine.cam.transform, false);
        handL = MakeHand("HandL");
        handR = MakeHand("HandR");
    }

    // 작업복 소매와 흰 목장갑
    Transform MakeHand(string name)
    {
        var h = new GameObject(name).transform;
        h.SetParent(hands, false);
        Color jacket = new Color(0.33f, 0.40f, 0.50f), glove = new Color(0.94f, 0.94f, 0.90f);
        Part(h, PrimitiveType.Capsule, new Vector3(0f, 0f, -0.15f), new Vector3(0.085f, 0.20f, 0.085f), Quaternion.Euler(90f, 0f, 0f), jacket);   // 소매
        Part(h, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.01f), new Vector3(0.088f, 0.012f, 0.088f), Quaternion.Euler(90f, 0f, 0f), new Color(0.10f, 0.10f, 0.12f)); // 소맷단
        Part(h, PrimitiveType.Sphere, new Vector3(0f, -0.005f, 0.07f), new Vector3(0.095f, 0.070f, 0.14f), Quaternion.identity, glove);           // 손바닥
        for (int i = 0; i < 4; i++) // 손가락
            Part(h, PrimitiveType.Capsule, new Vector3(-0.033f + i * 0.022f, -0.004f, 0.165f), new Vector3(0.020f, 0.045f, 0.020f), Quaternion.Euler(90f, 0f, 0f), glove);
        Part(h, PrimitiveType.Capsule, new Vector3(0.055f, -0.01f, 0.09f), new Vector3(0.022f, 0.05f, 0.022f), Quaternion.Euler(90f, 0f, -25f), glove); // 엄지
        return h;
    }

    static void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Color c)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        MachineBuilder.Paint(go, c);
        go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    // 시점 전환 버튼과 안내 (위쪽 가운데)
    void DrawViewToggle(float W)
    {
        if (sHint == null)
        {
            sHint = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter };
            sHint.normal.textColor = Color.white;
        }
        float cx = 488 + (W - 1076) / 2f;
        if (GUI.Button(new Rect(cx - 130, 24, 260, 44), firstPerson ? "전체 보기로  (V)" : "1인칭으로  (V)", sBtn)) firstPerson = !firstPerson;
        if (!docSeen && !showDoc)
        {
            // 처음에는 눈에 띄게 숨 쉬듯 번지는 테두리로 "자료 보기"를 알려 준다
            float k = Mathf.Repeat(Time.time, 1.4f) / 1.4f;
            float g = 4f + 18f * k;
            Color old = GUI.color;
            GUI.color = new Color(1f, 0.6f, 0.2f, 0.55f * (1f - k));
            GUI.DrawTexture(new Rect(cx + 140 - g, 24 - g, 130 + 2 * g, 44 + 2 * g), white);
            GUI.color = old;
        }
        if (GUI.Button(new Rect(cx + 140, 24, 130, 44), showDoc ? "자료 닫기" : "자료 보기", showDoc ? sBtnOn : sBtn)) { showDoc = !showDoc; docSeen = true; }
        if (firstPerson && blend > 0.9f)
        {
            Fill(new Rect(cx - 150, 74, 300, 30), new Color(0f, 0f, 0f, 0.5f));
            GUI.Label(new Rect(cx - 150, 74, 300, 30), "빈 곳을 드래그하면 고개가 돌아가요", sHint);
        }
    }
}
