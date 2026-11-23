using System.Collections.Generic;
using UnityEngine;

// 실감을 높이는 부분: 절차적 질감, 발광 조명 기구, 현장 소품, 사람 변주, 기계 소리.
// 외부 에셋 없이 코드로만 만든다 (무료, 빌드에 따로 넣을 파일 없음).
public static partial class MachineBuilder
{
    // ---------- 사람마다 다르게 ----------
    static readonly Color[] WorkerSkins =
    {
        new Color(0.86f, 0.70f, 0.58f), new Color(0.78f, 0.60f, 0.47f), new Color(0.90f, 0.75f, 0.64f), new Color(0.72f, 0.54f, 0.42f)
    };
    static readonly Color[] WorkerHairs =
    {
        new Color(0.08f, 0.07f, 0.07f), new Color(0.16f, 0.12f, 0.10f), new Color(0.30f, 0.28f, 0.27f), new Color(0.10f, 0.09f, 0.08f)
    };
    static readonly float[] WorkerHeights = { 1.00f, 1.05f, 0.97f, 1.02f };
    static readonly float[] WorkerBuild = { 1.00f, 1.08f, 0.94f, 1.03f };

    static Renderer lastBeacon;

    // ---------- 절차적 질감 (이음매 없이 반복되는 노이즈) ----------
    static float H(int x, int y, int s)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + s * 1274126177;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }

    // 가로 px, 세로 py 칸마다 반복되는 값 노이즈
    static float VN(float x, float y, int px, int py, int s)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        int xa = ((x0 % px) + px) % px, xb = (xa + 1) % px;
        int ya = ((y0 % py) + py) % py, yb = (ya + 1) % py;
        float a = Mathf.Lerp(H(xa, ya, s), H(xb, ya, s), fx);
        float b = Mathf.Lerp(H(xa, yb, s), H(xb, yb, s), fx);
        return Mathf.Lerp(a, b, fy);
    }

    static float Fbm(float u, float v, int basePeriod, int s)
    {
        float sum = 0f, amp = 0.5f, tot = 0f;
        int p = basePeriod;
        for (int o = 0; o < 4; o++)
        {
            sum += amp * VN(u * p, v * p, p, p, s + o * 7);
            tot += amp;
            amp *= 0.5f;
            p *= 2;
        }
        return sum / tot;
    }

    static Texture2D MakeTex(int n, System.Func<float, float, float> gray)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true);
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float g = Mathf.Clamp01(gray(x / (float)n, y / (float)n));
                px[y * n + x] = new Color(g, g, g, 1f);
            }
        t.SetPixels(px);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Bilinear;
        t.anisoLevel = 4;
        t.Apply(true);
        return t;
    }

    // 회색 명암만 담고, 실제 색은 재질 색과 곱해진다
    static class Tx
    {
        static Texture2D floor, wall, steel, paint, wood, paper, fabric;

        // 에폭시 바닥: 얼룩, 바퀴 자국, 5m마다 줄눈
        public static Texture2D Floor => floor ? floor : (floor = MakeTex(256, (u, v) =>
        {
            float g = 0.86f + (Fbm(u, v, 4, 1) - 0.5f) * 0.26f + (H((int)(u * 256), (int)(v * 256), 3) - 0.5f) * 0.05f;
            float streak = VN(u * 3f, v * 40f, 3, 40, 9);              // 한 방향으로 번진 바퀴 자국
            if (streak > 0.72f) g -= (streak - 0.72f) * 0.45f;
            if (u < 0.006f || v < 0.006f) g *= 0.70f;                  // 줄눈
            return g;
        }));

        public static Texture2D Wall => wall ? wall : (wall = MakeTex(256, (u, v) =>
        {
            float g = 0.93f + (Fbm(u, v, 3, 21) - 0.5f) * 0.10f;
            if (u < 0.012f) g *= 0.82f;                                 // 패널 이음선
            if (v > 0.45f && v < 0.456f) g *= 0.9f;
            return g;
        }));

        // 솔질한 금속: 가로 결
        public static Texture2D Steel => steel ? steel : (steel = MakeTex(256, (u, v) =>
            0.80f + (VN(u * 2f, v * 96f, 2, 96, 31) - 0.5f) * 0.26f + (Fbm(u, v, 4, 33) - 0.5f) * 0.10f));

        // 도장면: 잔 얼룩과 드문 긁힘
        public static Texture2D Paint => paint ? paint : (paint = MakeTex(256, (u, v) =>
        {
            float g = 0.94f + (Fbm(u, v, 6, 41) - 0.5f) * 0.10f;
            float scratch = VN(u * 24f, v * 2f, 24, 2, 47);
            if (scratch > 0.9f) g += 0.06f;
            return g;
        }));

        // 나무 결: 세로 줄무늬
        public static Texture2D Wood => wood ? wood : (wood = MakeTex(256, (u, v) =>
            0.72f + (VN(u * 3f, v * 48f, 3, 48, 51) - 0.5f) * 0.38f + (Fbm(u, v, 4, 53) - 0.5f) * 0.12f));

        // 천: 촘촘한 직조 무늬
        public static Texture2D Fabric => fabric ? fabric : (fabric = MakeTex(128, (u, v) =>
            0.86f + Mathf.Sin(u * Mathf.PI * 2f * 48f) * Mathf.Sin(v * Mathf.PI * 2f * 48f) * 0.06f + (Fbm(u, v, 8, 81) - 0.5f) * 0.12f));

        // 포대·종이 상자: 잔 주름
        public static Texture2D Paper => paper ? paper : (paper = MakeTex(256, (u, v) =>
            0.88f + (Fbm(u, v, 10, 61) - 0.5f) * 0.18f + (H((int)(u * 256), (int)(v * 256), 5) - 0.5f) * 0.04f));
    }

    // 질감, 금속성, 광택을 입힌다. tx, ty는 반복 횟수
    static Renderer Skin(Renderer r, Texture2D t, float tx, float ty, float metal, float smooth)
    {
        var mat = r.material;
        mat.mainTexture = t;
        mat.mainTextureScale = new Vector2(tx, ty);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metal);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smooth);
        return r;
    }

    // 스스로 빛나게 한다 (어두운 공장에서 표시등·화면·조명이 또렷하다)
    static Renderer Glow(Renderer r, Color emission, float k)
    {
        var mat = r.material;
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mat.SetColor("_EmissionColor", emission * k);
        return r;
    }

    public static void SetGlow(Renderer r, Color emission)
    {
        r.material.SetColor("_EmissionColor", emission);
    }

    static Light MakeLight(Transform parent, string name, LightType type, Vector3 pos, Color c, float intensity, float range, LightShadows shadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = type;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
        l.shadows = shadows;
        return l;
    }

    // Z축 방향 원기둥
    static Renderer RodZ(Transform parent, string name, Vector3 pos, float length, float diameter, Color c)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        go.transform.localScale = new Vector3(diameter, length / 2f, diameter);
        return Paint(go, c);
    }

    // 세로(Y축) 원기둥
    static Renderer RodY(Transform parent, string name, Vector3 pos, float height, float diameter, Color c)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(diameter, height / 2f, diameter);
        return Paint(go, c);
    }

    // ---------- 현장 디테일 ----------
    static void BuildDetails(Machine m)
    {
        var d = new GameObject("Details").transform;
        BuildFixtures(d);
        BuildPipes(d);
        BuildRailing(d);
        BuildShelf(d, new Vector3(8.6f, 0f, 5.6f));
        BuildToolCart(d, new Vector3(3.2f, 0f, -2.2f));
        BuildExtinguisher(m, d, new Vector3(-11.12f, 0f, 9.0f));
        BuildExtinguisher(m, d, new Vector3(11.12f, 0f, 9.0f));
        BuildExitDoor(m, d);
        BuildClock(d);
        BuildHazardTape(d);
        BuildCrates(d);
        BuildCabinet(d);
        BuildZones(m, d); // 휴게 구역, 흡연구역, 사물함, 지게차 등 (MachineBuilder.Zones.cs)
    }

    // 천장에 매단 형광등. 불빛은 작은 점광원과 바로 아래를 비추는 그림자 있는 조명으로 만든다
    static void BuildFixtures(Transform d)
    {
        var f = new GameObject("Fixtures").transform;
        f.SetParent(d, false);
        Color warm = new Color(1f, 0.96f, 0.88f);
        float[] zs = { -0.6f, 4.3f, 9.3f };
        float[] xs = { -5.5f, 0f, 5.5f };
        foreach (float z in zs)
            foreach (float x in xs)
            {
                Box(f, "Hanger", new Vector3(x - 0.9f, 5.8f, z), new Vector3(0.02f, 0.9f, 0.02f), Dark);
                Box(f, "Hanger", new Vector3(x + 0.9f, 5.8f, z), new Vector3(0.02f, 0.9f, 0.02f), Dark);
                Box(f, "Housing", new Vector3(x, 5.34f, z), new Vector3(2.5f, 0.09f, 0.34f), new Color(0.82f, 0.83f, 0.85f));
                Glow(Box(f, "Tube", new Vector3(x, 5.28f, z), new Vector3(2.3f, 0.04f, 0.2f), warm), warm, 2.2f);
            }
        // 불빛: 한가운데는 그림자 있는 스포트(작업자와 기계 아래에 그림자가 진다), 나머지는 점광원
        var spot = MakeLight(f, "KeySpot", LightType.Spot, new Vector3(0.4f, 5.2f, -1.4f), warm, 2.3f, 14f, LightShadows.Soft);
        spot.spotAngle = 78f;
        spot.transform.rotation = Quaternion.LookRotation(new Vector3(0.3f, 1.0f, 0.0f) - new Vector3(0.4f, 5.2f, -1.4f));
        MakeLight(f, "LampL", LightType.Point, new Vector3(-5.5f, 5.0f, 4.3f), warm, 1.2f, 11f, LightShadows.None);
        MakeLight(f, "LampM", LightType.Point, new Vector3(0f, 5.0f, 4.3f), warm, 1.2f, 11f, LightShadows.None);
        MakeLight(f, "LampR", LightType.Point, new Vector3(5.5f, 5.0f, 4.3f), warm, 1.2f, 11f, LightShadows.None);
        MakeLight(f, "LampBackL", LightType.Point, new Vector3(-5.5f, 5.0f, 9.3f), warm, 1.0f, 11f, LightShadows.None);
        MakeLight(f, "LampBackR", LightType.Point, new Vector3(5.5f, 5.0f, 9.3f), warm, 1.0f, 11f, LightShadows.None);
        // 도어 라인 작업대 위
        var s2 = MakeLight(f, "DoorSpot", LightType.Spot, new Vector3(0.6f, 5.6f, 12.4f), new Color(1f, 0.97f, 0.9f), 2.0f, 12f, LightShadows.Soft);
        s2.spotAngle = 75f;
        s2.transform.rotation = Quaternion.LookRotation(new Vector3(0.6f, 1.5f, 14.4f) - new Vector3(0.6f, 5.6f, 12.4f));
    }

    // 뒤쪽 벽을 따라 지나가는 배관 (녹색 용수, 빨강 소화, 파랑 에어)
    static void BuildPipes(Transform d)
    {
        var p = new GameObject("Pipes").transform;
        p.SetParent(d, false);
        Skin(Rod(p, "PipeWater", new Vector3(0, 3.40f, 17.66f), 27f, 0.16f, new Color(0.18f, 0.55f, 0.32f)), Tx.Steel, 1f, 1f, 0.2f, 0.45f);
        Skin(Rod(p, "PipeFire", new Vector3(0, 3.08f, 17.68f), 27f, 0.12f, new Color(0.78f, 0.12f, 0.10f)), Tx.Steel, 1f, 1f, 0.2f, 0.45f);
        Skin(Rod(p, "PipeAir", new Vector3(0, 2.80f, 17.70f), 27f, 0.10f, new Color(0.20f, 0.42f, 0.78f)), Tx.Steel, 1f, 1f, 0.2f, 0.45f);
        for (int i = -4; i <= 4; i++)
            Box(p, "Bracket", new Vector3(i * 3f, 3.1f, 17.78f), new Vector3(0.07f, 0.9f, 0.14f), Dark);
    }

    // 뒤쪽 설비 앞의 안전 난간
    static void BuildRailing(Transform d)
    {
        var r = new GameObject("Railing").transform;
        r.SetParent(d, false);
        const float z = 5.7f;
        for (int i = -4; i <= 4; i++)
            Box(r, "Post", new Vector3(i * 1.5f, 0.52f, z), new Vector3(0.06f, 1.05f, 0.06f), SafetyYellow);
        Rod(r, "RailTop", new Vector3(0, 1.0f, z), 12f, 0.045f, SafetyYellow);
        Rod(r, "RailMid", new Vector3(0, 0.55f, z), 12f, 0.04f, SafetyYellow);
        Box(r, "ToeBoard", new Vector3(0, 0.07f, z), new Vector3(12f, 0.14f, 0.03f), new Color(0.12f, 0.12f, 0.13f));
    }

    // 자재 선반과 상자
    static void BuildShelf(Transform d, Vector3 pos)
    {
        var s = new GameObject("Shelf").transform;
        s.SetParent(d, false);
        s.position = pos;
        Color orange = new Color(0.90f, 0.42f, 0.08f);
        Color gray = new Color(0.62f, 0.64f, 0.67f);
        foreach (float x in new[] { -0.9f, 0.9f })
            foreach (float z in new[] { -0.3f, 0.3f })
                Box(s, "Upright", new Vector3(x, 1.15f, z), new Vector3(0.06f, 2.3f, 0.06f), orange);
        float[] ys = { 0.35f, 1.05f, 1.75f };
        foreach (float y in ys)
            Skin(Box(s, "Board", new Vector3(0, y, 0), new Vector3(1.9f, 0.05f, 0.7f), gray), Tx.Steel, 1f, 1f, 0.3f, 0.4f);
        var rng = new System.Random(3);
        foreach (float y in ys)
        {
            float x = -0.8f;
            while (x < 0.7f)
            {
                float w = 0.28f + (float)rng.NextDouble() * 0.25f;
                float h = 0.22f + (float)rng.NextDouble() * 0.30f;
                float tone = 0.62f + (float)rng.NextDouble() * 0.12f;
                Skin(Box(s, "Carton", new Vector3(x + w / 2f, y + 0.025f + h / 2f, 0), new Vector3(w, h, 0.5f), new Color(tone, tone * 0.78f, tone * 0.5f)), Tx.Paper, 1f, 1f, 0f, 0.05f);
                x += w + 0.04f;
            }
        }
    }

    // 바퀴 달린 공구함
    static void BuildToolCart(Transform d, Vector3 pos)
    {
        var c = new GameObject("ToolCart").transform;
        c.SetParent(d, false);
        c.position = pos;
        Skin(Box(c, "Body", new Vector3(0, 0.55f, 0), new Vector3(0.9f, 0.8f, 0.5f), new Color(0.72f, 0.12f, 0.10f)), Tx.Paint, 1f, 1f, 0.15f, 0.5f);
        for (int i = 0; i < 3; i++)
        {
            Box(c, "DrawerLine", new Vector3(0, 0.30f + i * 0.22f, -0.255f), new Vector3(0.84f, 0.012f, 0.01f), new Color(0.12f, 0.03f, 0.03f));
            Box(c, "Handle", new Vector3(0, 0.40f + i * 0.22f, -0.265f), new Vector3(0.30f, 0.025f, 0.025f), new Color(0.78f, 0.80f, 0.82f));
        }
        Box(c, "Top", new Vector3(0, 0.96f, 0), new Vector3(0.94f, 0.04f, 0.54f), new Color(0.22f, 0.23f, 0.25f));
        foreach (float x in new[] { -0.38f, 0.38f })
            foreach (float z in new[] { -0.2f, 0.2f })
            {
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = "Wheel";
                wheel.transform.SetParent(c, false);
                wheel.transform.localPosition = new Vector3(x, 0.07f, z);
                wheel.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                wheel.transform.localScale = new Vector3(0.14f, 0.025f, 0.14f);
                Paint(wheel, new Color(0.08f, 0.08f, 0.09f));
            }
        // 위에 놓인 공구
        Box(c, "ToolBox", new Vector3(-0.2f, 1.03f, 0), new Vector3(0.28f, 0.1f, 0.14f), SafetyYellow);
        Rod(c, "Wrench", new Vector3(0.18f, 0.99f, 0.05f), 0.32f, 0.03f, new Color(0.72f, 0.74f, 0.77f));
    }

    // 기둥에 붙은 소화기와 표지
    static void BuildExtinguisher(Machine m, Transform d, Vector3 pos)
    {
        var e = new GameObject("Extinguisher").transform;
        e.SetParent(d, false);
        e.position = pos;
        float side = pos.x < 0 ? 1f : -1f; // 기둥 안쪽을 향한다
        Box(e, "Sign", new Vector3(-side * 0.0f, 1.75f, 0.0f), new Vector3(0.3f, 0.3f, 0.02f), new Color(0.78f, 0.10f, 0.08f));
        RodY(e, "Tank", new Vector3(0, 0.38f, 0), 0.5f, 0.15f, new Color(0.80f, 0.10f, 0.08f));
        RodY(e, "Neck", new Vector3(0, 0.68f, 0), 0.08f, 0.07f, Dark);
        Box(e, "Nozzle", new Vector3(0.05f, 0.72f, 0), new Vector3(0.10f, 0.025f, 0.025f), Dark);
        m.labels.Add(new WorldLabel { position = new Vector3(pos.x, 1.75f, pos.z - 0.015f), text = "소화기", height = 0.10f, color = Color.white });
    }

    // 비상구: 초록 표시등이 켜진 문
    static void BuildExitDoor(Machine m, Transform d)
    {
        Box(d, "ExitDoor", new Vector3(9.5f, 1.3f, 17.70f), new Vector3(1.5f, 2.6f, 0.06f), new Color(0.42f, 0.44f, 0.47f));
        Box(d, "ExitDoorHandle", new Vector3(9.0f, 1.1f, 17.64f), new Vector3(0.05f, 0.2f, 0.04f), new Color(0.8f, 0.82f, 0.84f));
        Color green = new Color(0.05f, 0.75f, 0.30f);
        Glow(Box(d, "ExitSign", new Vector3(9.5f, 2.95f, 17.68f), new Vector3(0.9f, 0.28f, 0.05f), green), green, 1.6f);
        m.labels.Add(new WorldLabel { position = new Vector3(9.5f, 2.95f, 17.64f), text = "비상구", height = 0.20f, color = Color.white });
    }

    // 벽시계: 새벽 2시 50분 (야간 교대)
    static void BuildClock(Transform d)
    {
        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "ClockRim";
        rim.transform.SetParent(d, false);
        rim.transform.localPosition = new Vector3(-6.5f, 5.0f, 17.72f);
        rim.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        rim.transform.localScale = new Vector3(0.78f, 0.03f, 0.78f);
        Paint(rim, new Color(0.08f, 0.08f, 0.09f));
        var face = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        face.name = "ClockFace";
        face.transform.SetParent(d, false);
        face.transform.localPosition = new Vector3(-6.5f, 5.0f, 17.695f);
        face.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        face.transform.localScale = new Vector3(0.68f, 0.03f, 0.68f);
        Paint(face, new Color(0.96f, 0.96f, 0.94f));
        var hour = Box(d, "ClockHour", new Vector3(-6.5f, 5.0f, 17.68f), new Vector3(0.025f, 0.17f, 0.01f), Dark);
        hour.transform.RotateAround(new Vector3(-6.5f, 5.0f, 17.68f), Vector3.forward, -85f);
        hour.transform.position += Vector3.zero;
        var min = Box(d, "ClockMinute", new Vector3(-6.5f, 5.0f, 17.675f), new Vector3(0.018f, 0.26f, 0.01f), Dark);
        min.transform.RotateAround(new Vector3(-6.5f, 5.0f, 17.675f), Vector3.forward, 60f);
    }

    // 바닥의 위험 구역 테이프 (노랑·검정 줄무늬)
    static void BuildHazardTape(Transform d)
    {
        var t = new GameObject("HazardTape").transform;
        t.SetParent(d, false);
        for (int i = 0; i < 40; i++)
            Box(t, "Stripe", new Vector3(-7f + i * 0.35f + 0.175f, 0.012f, -2.7f), new Vector3(0.17f, 0.012f, 0.22f), i % 2 == 0 ? SafetyYellow : new Color(0.07f, 0.07f, 0.08f));
        // 기계 옆 볼라드
        foreach (float x in new[] { -6.3f, 6.3f })
        {
            RodY(t, "Bollard", new Vector3(x, 0.5f, -2.3f), 1.0f, 0.16f, SafetyYellow);
            RodY(t, "BollardBand", new Vector3(x, 0.62f, -2.3f), 0.12f, 0.165f, new Color(0.07f, 0.07f, 0.08f));
        }
    }

    // 파란 자재 상자와 회색 통
    static void BuildCrates(Transform d)
    {
        var c = new GameObject("Crates").transform;
        c.SetParent(d, false);
        Color blue = new Color(0.15f, 0.34f, 0.66f);
        Box(c, "Crate0", new Vector3(0.95f, 0.16f, -0.75f), new Vector3(0.62f, 0.32f, 0.42f), blue);
        var top = Box(c, "Crate1", new Vector3(0.97f, 0.48f, -0.74f), new Vector3(0.6f, 0.30f, 0.40f), blue);
        top.transform.localRotation = Quaternion.Euler(0, 6f, 0);
        Box(c, "CrateRim0", new Vector3(0.95f, 0.325f, -0.75f), new Vector3(0.64f, 0.03f, 0.44f), new Color(0.10f, 0.24f, 0.50f));
        var bin = RodY(c, "Bin", new Vector3(-3.4f, 0.32f, -1.1f), 0.64f, 0.42f, new Color(0.38f, 0.40f, 0.43f));
        Skin(bin, Tx.Steel, 1f, 1f, 0.3f, 0.35f);
        RodY(c, "BinRim", new Vector3(-3.4f, 0.65f, -1.1f), 0.04f, 0.45f, new Color(0.25f, 0.26f, 0.28f));
    }

    // 벽 쪽 분전반과 경고 표지
    static void BuildCabinet(Transform d)
    {
        Skin(Box(d, "Cabinet", new Vector3(-11f, 1.2f, 17.5f), new Vector3(1.2f, 2.4f, 0.4f), new Color(0.58f, 0.60f, 0.62f)), Tx.Paint, 1f, 1f, 0.2f, 0.35f);
        Box(d, "CabinetDoorLine", new Vector3(-11f, 1.2f, 17.295f), new Vector3(0.012f, 2.3f, 0.01f), new Color(0.2f, 0.2f, 0.22f));
        Box(d, "Warning", new Vector3(-11.3f, 1.7f, 17.29f), new Vector3(0.30f, 0.30f, 0.01f), SafetyYellow);
        Box(d, "WarningMark", new Vector3(-11.3f, 1.68f, 17.285f), new Vector3(0.03f, 0.16f, 0.01f), new Color(0.07f, 0.07f, 0.08f));
        Glow(Box(d, "PilotLamp", new Vector3(-10.7f, 1.8f, 17.29f), new Vector3(0.06f, 0.06f, 0.015f), new Color(0.1f, 0.8f, 0.2f)), new Color(0.1f, 0.8f, 0.2f), 1.4f);
    }

    // 사출기 앞 안전 가드, 유압 호스, 기름받이 트레이
    static void BuildMachineExtras(Transform parent, Color body)
    {
        // 가동부 앞 안전 가드 (얇은 노란 프레임)
        foreach (float x in new[] { -2.85f, -0.35f })
            Box(parent, "GuardPost", new Vector3(x, 1.2f, -0.86f), new Vector3(0.05f, 2.0f, 0.05f), SafetyYellow);
        Rod(parent, "GuardTop", new Vector3(-1.6f, 2.15f, -0.86f), 2.5f, 0.05f, SafetyYellow);
        Rod(parent, "GuardMid", new Vector3(-1.6f, 1.15f, -0.86f), 2.5f, 0.04f, SafetyYellow);
        // 유압 호스
        Rod(parent, "HoseA", new Vector3(-0.6f, 0.95f, 0.78f), 3.6f, 0.07f, new Color(0.08f, 0.08f, 0.09f));
        Rod(parent, "HoseB", new Vector3(-0.6f, 1.08f, 0.84f), 3.6f, 0.06f, new Color(0.10f, 0.10f, 0.11f));
        foreach (float x in new[] { -2.4f, 1.2f })
            Rod(parent, "HoseEnd", new Vector3(x, 1.0f, 0.8f), 0.14f, 0.11f, new Color(0.85f, 0.42f, 0.16f));
        // 기름받이 트레이
        Box(parent, "DripTray", new Vector3(0, 0.02f, 0), new Vector3(6.5f, 0.04f, 1.8f), new Color(0.12f, 0.12f, 0.13f));
        Box(parent, "OilStain", new Vector3(-1.8f, 0.045f, 0.2f), new Vector3(1.1f, 0.006f, 0.5f), new Color(0.05f, 0.05f, 0.06f));
    }
}

// 기계 소리: 외부 파일 없이 코드로 만든 낮은 웅웅거림과 금형이 닫히는 쿵 소리
public static class FactoryAudio
{
    static AudioSource amb, fx;
    static AudioClip thud;
    static bool muted;

    public static bool Muted => muted;

    public static void Init(GameObject camGo)
    {
        if (camGo.GetComponent<AudioListener>() == null) camGo.AddComponent<AudioListener>();
        amb = camGo.AddComponent<AudioSource>();
        amb.clip = Ambience();
        amb.loop = true;
        amb.volume = 0.20f;
        amb.spatialBlend = 0f;
        amb.playOnAwake = false;
        amb.Play();
        fx = camGo.AddComponent<AudioSource>();
        fx.spatialBlend = 0f;
        fx.playOnAwake = false;
        thud = Thud();
    }

    // 6초짜리 이음매 없는 반복: 전원 웅웅(50Hz와 배음), 낮은 럼블, 3초마다 압축기 바람 소리
    static AudioClip Ambience()
    {
        const int rate = 22050;
        const int total = rate * 6;
        const int fade = 2200;
        var data = new float[total];
        var rng = new System.Random(11);
        float lp1 = 0f, lp2 = 0f;
        for (int i = 0; i < total; i++)
        {
            float w = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp1 += (w - lp1) * 0.03f;
            lp2 += (lp1 - lp2) * 0.06f;
            float t = i / (float)rate;
            float hum = 0.50f * Mathf.Sin(2f * Mathf.PI * 50f * t) + 0.24f * Mathf.Sin(2f * Mathf.PI * 100f * t) + 0.10f * Mathf.Sin(2f * Mathf.PI * 150f * t);
            float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * t / 3f)), 6f);
            float hiss = w * 0.10f * pulse;
            data[i] = hum * 0.16f + lp2 * 2.4f + hiss;
        }
        // 끝과 처음을 섞어 이어 붙이는 자리의 딱 소리를 없앤다
        for (int k = 0; k < fade; k++)
        {
            float a = k / (float)fade;
            data[k] = data[k] * a + data[total - fade + k] * (1f - a);
        }
        int n = total - fade;
        var trimmed = new float[n];
        System.Array.Copy(data, trimmed, n);
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(trimmed[i]));
        for (int i = 0; i < n; i++) trimmed[i] = trimmed[i] / peak * 0.7f;
        var clip = AudioClip.Create("FactoryAmbience", n, 1, rate, false);
        clip.SetData(trimmed, 0);
        return clip;
    }

    // 금형이 닫히는 낮은 쿵 + 짧은 금속 부딪힘
    static AudioClip Thud()
    {
        const int rate = 22050;
        int n = (int)(rate * 0.30f);
        var data = new float[n];
        var rng = new System.Random(5);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float body = Mathf.Sin(2f * Mathf.PI * 58f * t * (1f - t * 0.7f)) * Mathf.Exp(-t * 15f) * 0.9f;
            float click = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 70f) * 0.35f;
            float clack = Mathf.Sin(2f * Mathf.PI * 820f * t) * Mathf.Exp(-t * 42f) * 0.18f;
            data[i] = body + click + clack;
        }
        var clip = AudioClip.Create("MoldThud", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static void PlayThud(float volume)
    {
        if (fx == null || thud == null || muted) return;
        fx.pitch = 0.92f + Random.value * 0.16f;
        fx.PlayOneShot(thud, volume);
    }

    public static void Toggle()
    {
        muted = !muted;
        if (amb != null) amb.mute = muted;
        if (fx != null) fx.mute = muted;
    }
}
