using UnityEngine;

// 공장 한쪽의 생활 공간과 설비: 자판기, 휴게 테이블, 흡연구역, 사물함, 게시판, 지게차, 벽 조명.
// 빈 벽과 바닥이 휑하지 않게 현장에 있을 법한 것들을 채운다. 모두 기본 도형으로 만든다.
public static partial class MachineBuilder
{
    // 물건 하나를 담는 묶음. 앞쪽은 로컬 +Z 이고, yaw로 돌려서 벽을 마주 보게 한다 (왼쪽 벽은 +90, 오른쪽 벽은 -90)
    static Transform Group(Transform parent, string name, Vector3 pos, float yaw)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = pos;
        t.rotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }

    static void BuildZones(Machine m, Transform d)
    {
        var z = new GameObject("Zones").transform;
        z.SetParent(d, false);
        BuildSideWallLights(z);
        BuildRestArea(m, z);
        BuildSmokingArea(m, z);
        BuildLockers(m, z, new Vector3(-13.55f, 0f, 0.4f));
        BuildBulletin(m, z, new Vector3(-13.78f, 1.75f, 6.4f));
        BuildSafetyBoard(m, z, new Vector3(-13.78f, 3.3f, -1.6f));
        BuildForklift(z, new Vector3(9.8f, 0f, -1.4f), 200f);
        BuildFinishedGoods(z, new Vector3(11.4f, 0f, 2.2f));
        BuildDirectionSign(m, z, new Vector3(-6.4f, 3.4f, -3.6f));
        BuildWalkLine(z);
        BuildDecor(m, z);
        BuildDocProps(m, z);
    }

    // 양쪽 벽에 일정한 간격으로 달린 벽등 (벽이 어둠 속에 묻히지 않게)
    static void BuildSideWallLights(Transform z)
    {
        var w = new GameObject("WallLights").transform;
        w.SetParent(z, false);
        Color warm = new Color(1f, 0.93f, 0.80f);
        for (int i = 0; i < 8; i++)
        {
            float zz = -7f + i * 3.4f;
            foreach (float x in new[] { -13.72f, 13.72f })
            {
                if (x < 0f && zz > -4.2f && zz < 0.4f) continue; // 무재해 현황판이 걸린 자리
                Box(w, "WallBracket", new Vector3(x, 3.05f, zz), new Vector3(0.10f, 0.16f, 1.3f), Dark);
                Glow(Box(w, "WallTube", new Vector3(x * 0.995f, 3.05f, zz), new Vector3(0.06f, 0.07f, 1.1f), warm), warm, 1.6f);
            }
        }
        MakeLight(w, "LeftWash", LightType.Point, new Vector3(-12.4f, 3.0f, 3.0f), warm, 1.0f, 12f, LightShadows.None);
        MakeLight(w, "RightWash", LightType.Point, new Vector3(12.4f, 3.0f, 3.0f), warm, 1.0f, 12f, LightShadows.None);
    }

    // 휴게 구역: 자판기 둘, 정수기, 분리수거함, 테이블과 의자
    static void BuildRestArea(Machine m, Transform z)
    {
        var rest = new GameObject("RestArea").transform;
        rest.SetParent(z, false);
        // 왼쪽 벽을 향한 자판기 두 대
        Vending(m, rest, new Vector3(-13.4f, 0f, -7.6f), new Color(0.78f, 0.12f, 0.10f), "음료");
        Vending(m, rest, new Vector3(-13.4f, 0f, -6.5f), new Color(0.12f, 0.34f, 0.70f), "커피·간식");
        WaterDispenser(rest, new Vector3(-13.5f, 0f, -5.5f));
        // 분리수거함 셋
        Color[] lid = { new Color(0.15f, 0.45f, 0.75f), new Color(0.15f, 0.62f, 0.30f), new Color(0.50f, 0.52f, 0.55f) };
        for (int i = 0; i < 3; i++)
        {
            var b = Group(rest, "Bin" + i, new Vector3(-13.4f, 0f, -4.8f + i * 0.45f), 90f);
            RodY(b, "Body", new Vector3(0, 0.40f, 0), 0.8f, 0.38f, new Color(0.30f, 0.32f, 0.35f));
            RodY(b, "Lid", new Vector3(0, 0.82f, 0), 0.05f, 0.41f, lid[i]);
            Box(b, "Slot", new Vector3(0, 0.84f, 0.16f), new Vector3(0.20f, 0.03f, 0.04f), Dark);
        }
        m.labels.Add(new WorldLabel { position = new Vector3(-13.78f, 2.35f, -4.6f), text = "분리수거", height = 0.16f, color = Color.white });

        // 휴게 테이블과 의자
        Vector3 tc = new Vector3(-11.0f, 0f, -4.0f);
        Skin(Box(rest, "TableTop", tc + new Vector3(0, 0.74f, 0), new Vector3(1.5f, 0.05f, 0.85f), new Color(0.80f, 0.74f, 0.62f)), Tx.Wood, 1f, 1f, 0f, 0.3f);
        foreach (float dx in new[] { -0.65f, 0.65f })
            foreach (float dz in new[] { -0.34f, 0.34f })
                Box(rest, "TableLeg", tc + new Vector3(dx, 0.36f, dz), new Vector3(0.05f, 0.72f, 0.05f), new Color(0.25f, 0.26f, 0.28f));
        // 테이블 위: 종이컵과 음료 캔
        RodY(rest, "Cup", tc + new Vector3(-0.35f, 0.82f, 0.1f), 0.1f, 0.07f, new Color(0.95f, 0.95f, 0.92f));
        RodY(rest, "Can", tc + new Vector3(0.3f, 0.84f, -0.12f), 0.12f, 0.065f, new Color(0.78f, 0.12f, 0.10f));
        Box(rest, "Magazine", tc + new Vector3(0.0f, 0.77f, 0.18f), new Vector3(0.30f, 0.012f, 0.22f), new Color(0.15f, 0.40f, 0.70f));
        Vector3[] seats = { new Vector3(-0.45f, 0, -0.75f), new Vector3(0.45f, 0, -0.75f), new Vector3(-0.45f, 0, 0.75f), new Vector3(0.45f, 0, 0.75f) };
        foreach (Vector3 s in seats)
        {
            RodY(rest, "StoolSeat", tc + s + new Vector3(0, 0.46f, 0), 0.06f, 0.34f, new Color(0.18f, 0.35f, 0.58f));
            RodY(rest, "StoolLeg", tc + s + new Vector3(0, 0.22f, 0), 0.44f, 0.05f, new Color(0.25f, 0.26f, 0.28f));
            RodY(rest, "StoolBase", tc + s + new Vector3(0, 0.02f, 0), 0.03f, 0.30f, new Color(0.20f, 0.21f, 0.23f));
        }
        m.labels.Add(new WorldLabel { position = new Vector3(-13.78f, 3.0f, -6.9f), text = "휴게실", height = 0.26f, color = Color.white });
        // 자판기 불빛이 번지는 느낌과 테이블 위 펜던트 등
        MakeLight(rest, "VendingGlow", LightType.Point, new Vector3(-12.3f, 1.4f, -7.0f), new Color(0.80f, 0.92f, 1f), 1.0f, 5.5f, LightShadows.None);
        Box(rest, "PendantCord", tc + new Vector3(0, 4.4f, 0), new Vector3(0.015f, 1.6f, 0.015f), Dark);
        RodY(rest, "PendantShade", tc + new Vector3(0, 3.55f, 0), 0.12f, 0.5f, new Color(0.85f, 0.85f, 0.82f));
        Glow(RodY(rest, "PendantBulb", tc + new Vector3(0, 3.47f, 0), 0.03f, 0.3f, new Color(1f, 0.95f, 0.8f)), new Color(1f, 0.92f, 0.75f), 2f);
        MakeLight(rest, "TableLamp", LightType.Point, tc + new Vector3(0, 3.2f, 0), new Color(1f, 0.92f, 0.78f), 1.2f, 6.5f, LightShadows.None);
    }

    static void Vending(Machine m, Transform parent, Vector3 pos, Color body, string title)
    {
        var v = Group(parent, "Vending", pos, 90f);
        Skin(Box(v, "Body", new Vector3(0, 0.93f, 0), new Vector3(0.95f, 1.86f, 0.80f), body), Tx.Paint, 1f, 1f, 0.15f, 0.55f);
        Glow(Box(v, "Header", new Vector3(0, 1.74f, 0.41f), new Vector3(0.93f, 0.20f, 0.02f), new Color(0.95f, 0.95f, 0.92f)), new Color(0.95f, 0.95f, 0.92f), 1.1f);
        Glow(Box(v, "Window", new Vector3(-0.08f, 1.06f, 0.41f), new Vector3(0.66f, 1.15f, 0.02f), new Color(0.80f, 0.90f, 0.98f)), new Color(0.80f, 0.90f, 0.98f), 0.9f);
        Color[] cans = { new Color(0.85f, 0.15f, 0.10f), new Color(0.15f, 0.45f, 0.80f), new Color(0.95f, 0.75f, 0.10f), new Color(0.18f, 0.60f, 0.30f), new Color(0.90f, 0.90f, 0.88f) };
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 4; c++)
                Box(v, "Item", new Vector3(-0.28f + c * 0.14f, 1.45f - r * 0.27f, 0.43f), new Vector3(0.09f, 0.17f, 0.03f), cans[(r * 3 + c * 2) % cans.Length]);
        for (int r = 0; r < 4; r++) // 칸 막이 선반
            Box(v, "Shelf", new Vector3(-0.08f, 1.34f - r * 0.27f, 0.425f), new Vector3(0.66f, 0.012f, 0.02f), new Color(0.35f, 0.38f, 0.42f));
        Box(v, "Keypad", new Vector3(0.36f, 1.2f, 0.41f), new Vector3(0.16f, 0.30f, 0.02f), new Color(0.12f, 0.12f, 0.14f));
        for (int i = 0; i < 6; i++)
            Box(v, "Key", new Vector3(0.32f + (i % 2) * 0.07f, 1.30f - (i / 2) * 0.08f, 0.425f), new Vector3(0.05f, 0.05f, 0.01f), new Color(0.75f, 0.77f, 0.80f));
        Glow(Box(v, "Display", new Vector3(0.36f, 1.38f, 0.425f), new Vector3(0.12f, 0.04f, 0.01f), new Color(1f, 0.25f, 0.15f)), new Color(1f, 0.25f, 0.15f), 1.6f);
        Box(v, "CoinSlot", new Vector3(0.36f, 0.95f, 0.415f), new Vector3(0.10f, 0.14f, 0.02f), new Color(0.65f, 0.67f, 0.70f));
        Box(v, "Outlet", new Vector3(-0.08f, 0.22f, 0.41f), new Vector3(0.55f, 0.20f, 0.03f), new Color(0.06f, 0.06f, 0.07f));
        m.labels.Add(new WorldLabel { position = pos + new Vector3(0.43f, 1.74f, 0f), text = title, height = 0.12f, color = new Color(0.10f, 0.10f, 0.12f) });
    }

    static void WaterDispenser(Transform parent, Vector3 pos)
    {
        var w = Group(parent, "WaterDispenser", pos, 90f);
        Skin(Box(w, "Body", new Vector3(0, 0.52f, 0), new Vector3(0.34f, 1.04f, 0.34f), new Color(0.86f, 0.88f, 0.90f)), Tx.Paint, 1f, 1f, 0.1f, 0.5f);
        RodY(w, "Bottle", new Vector3(0, 1.28f, 0), 0.42f, 0.26f, new Color(0.55f, 0.78f, 0.95f));
        Glow(Box(w, "HotLight", new Vector3(-0.07f, 0.84f, 0.175f), new Vector3(0.05f, 0.025f, 0.01f), new Color(1f, 0.25f, 0.15f)), new Color(1f, 0.25f, 0.15f), 1.5f);
        Glow(Box(w, "ColdLight", new Vector3(0.07f, 0.84f, 0.175f), new Vector3(0.05f, 0.025f, 0.01f), new Color(0.2f, 0.6f, 1f)), new Color(0.2f, 0.6f, 1f), 1.5f);
        Box(w, "Tray", new Vector3(0, 0.46f, 0.1f), new Vector3(0.26f, 0.025f, 0.2f), new Color(0.35f, 0.37f, 0.40f));
    }

    // 흡연구역: 노란 선으로 구획한 바닥, 재떨이, 벤치, 표지판, 환풍기
    static void BuildSmokingArea(Machine m, Transform z)
    {
        var s = new GameObject("SmokingArea").transform;
        s.SetParent(z, false);
        Vector3 c = new Vector3(-9.0f, 0f, -7.2f);
        const float w = 3.6f, dpt = 2.4f;
        Color yellow = SafetyYellow;
        Box(s, "EdgeN", c + new Vector3(0, 0.012f, dpt / 2f), new Vector3(w, 0.012f, 0.10f), yellow);
        Box(s, "EdgeS", c + new Vector3(0, 0.012f, -dpt / 2f), new Vector3(w, 0.012f, 0.10f), yellow);
        Box(s, "EdgeW", c + new Vector3(-w / 2f, 0.012f, 0), new Vector3(0.10f, 0.012f, dpt), yellow);
        Box(s, "EdgeE", c + new Vector3(w / 2f, 0.012f, 0), new Vector3(0.10f, 0.012f, dpt), yellow);
        // 구역 바닥은 약간 다른 색으로
        Box(s, "AreaFloor", c + new Vector3(0, 0.006f, 0), new Vector3(w - 0.1f, 0.008f, dpt - 0.1f), new Color(0.23f, 0.27f, 0.27f));
        // 재떨이 기둥 둘
        foreach (float dx in new[] { -0.9f, 0.9f })
        {
            RodY(s, "AshStand", c + new Vector3(dx, 0.45f, 0.3f), 0.9f, 0.08f, new Color(0.25f, 0.26f, 0.28f));
            RodY(s, "AshBowl", c + new Vector3(dx, 0.93f, 0.3f), 0.10f, 0.30f, new Color(0.38f, 0.40f, 0.43f));
            RodY(s, "AshBase", c + new Vector3(dx, 0.02f, 0.3f), 0.04f, 0.34f, new Color(0.18f, 0.19f, 0.21f));
            Box(s, "AshSand", c + new Vector3(dx, 0.99f, 0.3f), new Vector3(0.20f, 0.01f, 0.20f), new Color(0.55f, 0.52f, 0.46f));
        }
        // 벤치
        Skin(Box(s, "BenchSeat", c + new Vector3(0, 0.45f, -0.8f), new Vector3(1.6f, 0.07f, 0.40f), new Color(0.60f, 0.46f, 0.30f)), Tx.Wood, 1f, 1f, 0f, 0.15f);
        foreach (float dx in new[] { -0.7f, 0.7f })
            Box(s, "BenchLeg", c + new Vector3(dx, 0.22f, -0.8f), new Vector3(0.06f, 0.44f, 0.34f), new Color(0.22f, 0.23f, 0.25f));
        // 표지판 기둥: "흡연구역"
        Box(s, "SignPost", c + new Vector3(w / 2f - 0.15f, 1.25f, dpt / 2f - 0.15f), new Vector3(0.07f, 2.5f, 0.07f), Dark);
        Color signGreen = new Color(0.03f, 0.52f, 0.25f);
        var board = Group(s, "SignBoard", c + new Vector3(w / 2f - 0.15f, 2.3f, dpt / 2f - 0.25f), 150f);
        Glow(Box(board, "Face", Vector3.zero, new Vector3(1.2f, 0.42f, 0.04f), signGreen), signGreen, 0.8f);
        m.labels.Add(new WorldLabel { position = c + new Vector3(w / 2f - 0.15f + 0.45f * 0.5f, 2.3f, dpt / 2f - 0.25f - 0.45f * 0.87f), text = "흡연구역", height = 0.24f, color = Color.white });
        // 벽의 환풍기와 안내
        var fan = Group(s, "Fan", new Vector3(-13.72f, 2.6f, -8.0f), 90f);
        Box(fan, "Frame", Vector3.zero, new Vector3(0.9f, 0.9f, 0.08f), new Color(0.55f, 0.57f, 0.60f));
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "FanDisc";
        disc.transform.SetParent(fan, false);
        disc.transform.localPosition = new Vector3(0, 0, 0.05f);
        disc.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        disc.transform.localScale = new Vector3(0.75f, 0.02f, 0.75f);
        Paint(disc, new Color(0.15f, 0.16f, 0.18f));
        for (int i = 0; i < 3; i++)
        {
            var blade = Box(fan, "Blade" + i, new Vector3(0, 0, 0.075f), new Vector3(0.62f, 0.08f, 0.01f), new Color(0.72f, 0.74f, 0.77f));
            blade.transform.localRotation = Quaternion.Euler(0, 0, i * 60f);
        }
        MakeLight(s, "SmokingLamp", LightType.Point, c + new Vector3(0, 3.0f, 0), new Color(1f, 0.82f, 0.55f), 1.0f, 6.5f, LightShadows.None);
        Box(s, "LampCord", c + new Vector3(0, 4.0f, 0), new Vector3(0.015f, 2.0f, 0.015f), Dark);
        Glow(RodY(s, "LampBulb", c + new Vector3(0, 2.98f, 0), 0.03f, 0.28f, new Color(1f, 0.85f, 0.6f)), new Color(1f, 0.8f, 0.5f), 2f);
    }

    // 탈의실 쪽 사물함 여섯 칸
    static void BuildLockers(Machine m, Transform z, Vector3 pos)
    {
        var l = new GameObject("Lockers").transform;
        l.SetParent(z, false);
        Color[] tones = { new Color(0.40f, 0.52f, 0.62f), new Color(0.46f, 0.58f, 0.68f) };
        for (int i = 0; i < 6; i++)
        {
            var g = Group(l, "Locker" + i, pos + new Vector3(0, 0, i * 0.52f), 90f);
            Skin(Box(g, "Body", new Vector3(0, 0.95f, 0), new Vector3(0.50f, 1.90f, 0.46f), tones[i % 2]), Tx.Paint, 1f, 1f, 0.25f, 0.4f);
            for (int k = 0; k < 3; k++)
                Box(g, "Vent", new Vector3(0, 1.62f - k * 0.05f, 0.235f), new Vector3(0.28f, 0.015f, 0.01f), new Color(0.12f, 0.15f, 0.18f));
            Box(g, "Handle", new Vector3(0.16f, 1.05f, 0.24f), new Vector3(0.03f, 0.14f, 0.03f), new Color(0.78f, 0.80f, 0.83f));
            Box(g, "Number", new Vector3(0, 1.78f, 0.236f), new Vector3(0.10f, 0.06f, 0.01f), Color.white);
        }
        Box(l, "Bench", new Vector3(-12.7f, 0.42f, 2.0f), new Vector3(0.38f, 0.06f, 2.6f), new Color(0.55f, 0.43f, 0.28f));
        m.labels.Add(new WorldLabel { position = new Vector3(-13.78f, 2.35f, 1.9f), text = "탈의실 · 사물함", height = 0.20f, color = Color.white });
    }

    // 게시판: 안전 수칙과 공지가 붙은 코르크판
    static void BuildBulletin(Machine m, Transform z, Vector3 pos)
    {
        var b = Group(z, "Bulletin", pos, 90f);
        Box(b, "Frame", Vector3.zero, new Vector3(2.0f, 1.2f, 0.04f), new Color(0.55f, 0.55f, 0.58f));
        Box(b, "Cork", new Vector3(0, 0, 0.015f), new Vector3(1.9f, 1.1f, 0.03f), new Color(0.66f, 0.50f, 0.32f));
        var rng = new System.Random(8);
        Color[] papers = { Color.white, new Color(0.98f, 0.92f, 0.55f), new Color(0.82f, 0.92f, 0.98f), new Color(0.98f, 0.82f, 0.82f) };
        for (int i = 0; i < 8; i++)
        {
            float x = -0.75f + (i % 4) * 0.5f + ((float)rng.NextDouble() - 0.5f) * 0.06f;
            float y = i < 4 ? 0.24f : -0.22f;
            var p = Box(b, "Paper", new Vector3(x, y, 0.04f), new Vector3(0.30f, 0.38f, 0.01f), papers[rng.Next(papers.Length)]);
            p.transform.localRotation = Quaternion.Euler(0, 0, ((float)rng.NextDouble() - 0.5f) * 8f);
            Box(b, "Pin", new Vector3(x, y + 0.16f, 0.05f), new Vector3(0.025f, 0.025f, 0.012f), new Color(0.85f, 0.15f, 0.12f));
        }
        m.labels.Add(new WorldLabel { position = pos + new Vector3(0.1f, 0.78f, 0f), text = "게시판 · 안전 수칙", height = 0.18f, color = Color.white });
    }

    // 무재해 현황판: 벽에 걸린 초록 전광 표지
    static void BuildSafetyBoard(Machine m, Transform z, Vector3 pos)
    {
        var b = Group(z, "SafetyBoard", pos, 90f);
        Box(b, "Frame", Vector3.zero, new Vector3(3.0f, 1.2f, 0.06f), new Color(0.12f, 0.14f, 0.16f));
        Color g = new Color(0.04f, 0.45f, 0.20f);
        Glow(Box(b, "Face", new Vector3(0, 0, 0.035f), new Vector3(2.85f, 1.05f, 0.02f), g), g, 0.9f);
        m.labels.Add(new WorldLabel { position = pos + new Vector3(0.1f, 0.18f, 0f), text = "무재해 127일째", height = 0.42f, color = Color.white });
        m.labels.Add(new WorldLabel { position = pos + new Vector3(0.1f, -0.28f, 0f), text = "안전은 나로부터", height = 0.20f, color = new Color(0.85f, 1f, 0.85f) });
    }

    // 지게차: 노란 차체, 보호 지붕, 마스트와 포크, 경광등
    static void BuildForklift(Transform z, Vector3 pos, float yaw)
    {
        var f = Group(z, "Forklift", pos, yaw);
        Color yellow = new Color(0.96f, 0.72f, 0.06f);
        Color gray = new Color(0.22f, 0.23f, 0.26f);
        Skin(Box(f, "Chassis", new Vector3(0, 0.58f, 0), new Vector3(1.05f, 0.55f, 1.7f), yellow), Tx.Paint, 1f, 1f, 0.15f, 0.45f);
        Box(f, "Counterweight", new Vector3(0, 0.66f, -0.95f), new Vector3(1.05f, 0.62f, 0.45f), gray);
        Box(f, "Hood", new Vector3(0, 0.98f, 0.25f), new Vector3(0.9f, 0.18f, 0.7f), yellow);
        Box(f, "Seat", new Vector3(0, 1.02f, -0.30f), new Vector3(0.46f, 0.10f, 0.46f), new Color(0.08f, 0.08f, 0.09f));
        Box(f, "SeatBack", new Vector3(0, 1.28f, -0.52f), new Vector3(0.44f, 0.46f, 0.08f), new Color(0.08f, 0.08f, 0.09f));
        var wheel = Box(f, "Steering", new Vector3(0, 1.35f, 0.18f), new Vector3(0.30f, 0.025f, 0.30f), Dark);
        wheel.transform.localRotation = Quaternion.Euler(-35f, 0, 0);
        Box(f, "SteerColumn", new Vector3(0, 1.17f, 0.20f), new Vector3(0.04f, 0.36f, 0.04f), Dark);
        // 보호 지붕
        foreach (float x in new[] { -0.46f, 0.46f })
        {
            Box(f, "GuardPostF", new Vector3(x, 1.65f, 0.55f), new Vector3(0.05f, 1.1f, 0.05f), gray);
            Box(f, "GuardPostR", new Vector3(x, 1.65f, -0.62f), new Vector3(0.05f, 1.1f, 0.05f), gray);
        }
        Box(f, "GuardRoof", new Vector3(0, 2.22f, -0.03f), new Vector3(1.08f, 0.05f, 1.3f), gray);
        for (int i = 0; i < 4; i++)
            Box(f, "RoofBar", new Vector3(0, 2.25f, -0.55f + i * 0.35f), new Vector3(1.05f, 0.025f, 0.03f), Dark);
        Glow(Box(f, "Beacon", new Vector3(0, 2.34f, -0.45f), new Vector3(0.12f, 0.12f, 0.12f), new Color(1f, 0.55f, 0.05f)), new Color(1f, 0.5f, 0.05f), 1.5f);
        // 마스트와 포크
        foreach (float x in new[] { -0.22f, 0.22f })
            Box(f, "Mast", new Vector3(x, 1.15f, 1.0f), new Vector3(0.07f, 2.1f, 0.10f), gray);
        Box(f, "MastTop", new Vector3(0, 2.15f, 1.0f), new Vector3(0.5f, 0.06f, 0.10f), gray);
        Box(f, "Carriage", new Vector3(0, 0.55f, 1.06f), new Vector3(0.78f, 0.45f, 0.06f), gray);
        foreach (float x in new[] { -0.26f, 0.26f })
            Box(f, "Fork", new Vector3(x, 0.10f, 1.62f), new Vector3(0.10f, 0.05f, 1.15f), new Color(0.35f, 0.36f, 0.39f));
        // 바퀴
        foreach (float x in new[] { -0.55f, 0.55f })
        {
            WheelAt(f, new Vector3(x, 0.30f, 0.55f), 0.60f, 0.20f);
            WheelAt(f, new Vector3(x, 0.22f, -0.75f), 0.44f, 0.16f);
        }
    }

    static void WheelAt(Transform parent, Vector3 pos, float diameter, float width)
    {
        var w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        w.name = "Wheel";
        w.transform.SetParent(parent, false);
        w.transform.localPosition = pos;
        w.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        w.transform.localScale = new Vector3(diameter, width / 2f, diameter);
        Paint(w, new Color(0.07f, 0.07f, 0.08f));
    }

    // 완제품 팔레트: 랩으로 감싼 상자 더미
    static void BuildFinishedGoods(Transform z, Vector3 pos)
    {
        var g = new GameObject("FinishedGoods").transform;
        g.SetParent(z, false);
        for (int p = 0; p < 2; p++)
        {
            Vector3 o = pos + new Vector3(0, 0, p * 1.5f);
            Skin(Box(g, "Pallet", o + new Vector3(0, 0.08f, 0), new Vector3(1.2f, 0.16f, 1.0f), new Color(0.62f, 0.48f, 0.30f)), Tx.Wood, 2f, 1f, 0f, 0.08f);
            int layers = 3 + p;
            for (int i = 0; i < layers; i++)
                Skin(Box(g, "Carton", o + new Vector3(0, 0.16f + 0.2f + i * 0.4f, 0), new Vector3(1.1f, 0.38f, 0.9f), new Color(0.66f, 0.52f, 0.34f)), Tx.Paper, 1f, 1f, 0f, 0.2f);
            // 랩: 상자를 가리지 않게 얇은 띠 몇 줄로 표현한다
            for (int k = 0; k < layers * 2; k++)
                Skin(Box(g, "WrapBand", o + new Vector3(0, 0.16f + 0.1f + k * 0.2f, 0), new Vector3(1.13f, 0.025f, 0.93f), new Color(0.90f, 0.93f, 0.96f)), Tx.Paper, 1f, 1f, 0f, 0.85f);
        }
    }

    // 통로 위에 걸린 방향 안내판: 휴게실·흡연구역·자판기 가는 길
    static void BuildDirectionSign(Machine m, Transform z, Vector3 pos)
    {
        var s = Group(z, "DirectionSign", pos, 143f);
        Color g = new Color(0.03f, 0.50f, 0.24f);
        foreach (float x in new[] { -1.0f, 1.0f })
            Box(s, "Rod", new Vector3(x, 0.9f, 0), new Vector3(0.02f, 1.8f, 0.02f), Dark);
        Glow(Box(s, "Face", Vector3.zero, new Vector3(2.7f, 0.52f, 0.05f), g), g, 0.9f);
        m.labels.Add(new WorldLabel { position = pos + new Vector3(0.6f * 0.5f, 0f, -0.8f * 0.04f), text = "← 휴게실 · 흡연구역 · 자판기", height = 0.26f, color = Color.white });
    }

    // 바닥의 초록 보행 안전 선: 주 통로에서 휴게 구역으로 이어진다
    static void BuildWalkLine(Transform z)
    {
        var w = new GameObject("WalkLine").transform;
        w.SetParent(z, false);
        Color green = new Color(0.10f, 0.62f, 0.30f);
        Box(w, "Line", new Vector3(-8.2f, 0.011f, -3.4f), new Vector3(8.0f, 0.010f, 0.14f), green);
        Box(w, "LineDown", new Vector3(-12.1f, 0.011f, -5.0f), new Vector3(0.14f, 0.010f, 3.4f), green);
        for (int i = 0; i < 3; i++) // 방향 화살표
        {
            var a = Box(w, "ArrowA", new Vector3(-6.2f - i * 1.4f, 0.012f, -3.4f + 0.12f), new Vector3(0.45f, 0.010f, 0.10f), green);
            a.transform.localRotation = Quaternion.Euler(0, 35f, 0);
            var b = Box(w, "ArrowB", new Vector3(-6.2f - i * 1.4f, 0.012f, -3.4f - 0.12f), new Vector3(0.45f, 0.010f, 0.10f), green);
            b.transform.localRotation = Quaternion.Euler(0, -35f, 0);
        }
    }

    // ---------- 전체 꾸미기: 화분, 포스터, 검사대, 표지, 매트, 소품 ----------

    static void BuildDecor(Machine m, Transform z)
    {
        var d = new GameObject("Decor").transform;
        d.SetParent(z, false);
        Plant(d, new Vector3(-12.8f, 0f, -3.0f), 1.0f);
        Plant(d, new Vector3(-12.8f, 0f, -0.7f), 0.85f);
        Plant(d, new Vector3(12.9f, 0f, -4.6f), 1.1f);
        Plant(d, new Vector3(-5.6f, 0f, -7.9f), 0.9f);

        // 방진 매트: 주인공이 서 있는 자리 (피로를 줄이는 검은 고무 매트와 노란 가장자리)
        Vector3 mc = new Vector3(1.9f, 0f, -2.0f);
        Box(d, "Mat", mc + new Vector3(0, 0.014f, 0), new Vector3(1.4f, 0.028f, 1.0f), new Color(0.08f, 0.08f, 0.09f));
        Box(d, "MatEdgeN", mc + new Vector3(0, 0.03f, 0.5f), new Vector3(1.4f, 0.008f, 0.05f), SafetyYellow);
        Box(d, "MatEdgeS", mc + new Vector3(0, 0.03f, -0.5f), new Vector3(1.4f, 0.008f, 0.05f), SafetyYellow);

        BuildQcTable(m, d, new Vector3(5.8f, 0f, -4.8f));

        // 기계 가드의 경고 표지: 끼임 주의, 보호구 착용
        Box(d, "WarnBorder", new Vector3(-1.6f, 1.75f, -0.89f), new Vector3(0.36f, 0.32f, 0.01f), new Color(0.07f, 0.07f, 0.08f));
        Glow(Box(d, "WarnSign", new Vector3(-1.6f, 1.75f, -0.897f), new Vector3(0.32f, 0.28f, 0.01f), SafetyYellow), SafetyYellow, 0.35f);
        m.labels.Add(new WorldLabel { position = new Vector3(-1.6f, 1.75f, -0.91f), text = "끼임 주의", height = 0.10f, color = new Color(0.07f, 0.07f, 0.08f) });
        Glow(Box(d, "PpeSign", new Vector3(-2.35f, 1.75f, -0.897f), new Vector3(0.30f, 0.28f, 0.01f), new Color(0.10f, 0.36f, 0.72f)), new Color(0.10f, 0.36f, 0.72f), 0.4f);
        m.labels.Add(new WorldLabel { position = new Vector3(-2.35f, 1.75f, -0.91f), text = "보호구 착용", height = 0.09f, color = Color.white });

        // 전선 릴
        Vector3 rc = new Vector3(7.6f, 0f, -3.6f);
        RodZ(d, "ReelDiscA", rc + new Vector3(0, 0.42f, -0.22f), 0.04f, 0.84f, new Color(0.60f, 0.46f, 0.28f));
        RodZ(d, "ReelDiscB", rc + new Vector3(0, 0.42f, 0.22f), 0.04f, 0.84f, new Color(0.60f, 0.46f, 0.28f));
        RodZ(d, "ReelCore", rc + new Vector3(0, 0.42f, 0), 0.44f, 0.30f, new Color(0.50f, 0.38f, 0.22f));
        RodZ(d, "ReelCable", rc + new Vector3(0, 0.42f, 0), 0.40f, 0.62f, new Color(0.90f, 0.45f, 0.08f));
        Box(d, "ReelTail", rc + new Vector3(0.5f, 0.012f, 0.0f), new Vector3(1.2f, 0.024f, 0.04f), new Color(0.90f, 0.45f, 0.08f));

        // 소화전함 (오른쪽 벽)
        var fh = Group(d, "HoseCabinet", new Vector3(13.7f, 1.3f, -1.0f), -90f);
        Box(fh, "Body", Vector3.zero, new Vector3(0.9f, 1.0f, 0.22f), new Color(0.78f, 0.10f, 0.08f));
        Box(fh, "Glass", new Vector3(0, 0, 0.115f), new Vector3(0.72f, 0.80f, 0.01f), new Color(0.10f, 0.12f, 0.15f));
        Box(fh, "HoseCoil", new Vector3(0, 0, 0.125f), new Vector3(0.45f, 0.45f, 0.01f), new Color(0.82f, 0.72f, 0.45f));
        m.labels.Add(new WorldLabel { position = new Vector3(13.5f, 1.95f, -1.0f), text = "소화전", height = 0.14f, color = Color.white });

        // 걸레 양동이
        Vector3 bc = new Vector3(-12.7f, 0f, -1.8f);
        Box(d, "MopBucket", bc + new Vector3(0, 0.18f, 0), new Vector3(0.42f, 0.36f, 0.50f), new Color(0.95f, 0.78f, 0.10f));
        var stick = Box(d, "MopStick", bc + new Vector3(0.05f, 0.85f, 0.05f), new Vector3(0.03f, 1.3f, 0.03f), new Color(0.65f, 0.50f, 0.30f));
        stick.transform.localRotation = Quaternion.Euler(0, 0, 10f);
        Box(d, "MopHead", bc + new Vector3(0.18f, 0.40f, 0.05f), new Vector3(0.26f, 0.10f, 0.12f), new Color(0.90f, 0.90f, 0.88f));

        // 벽 포스터
        Poster(m, d, new Vector3(-13.78f, 2.2f, 9.5f), 90f, new Color(0.10f, 0.45f, 0.22f), "5S 정리정돈");
        Poster(m, d, new Vector3(-13.78f, 2.2f, 12.6f), 90f, new Color(0.80f, 0.15f, 0.10f), "안전모 착용");
        Poster(m, d, new Vector3(13.78f, 2.2f, -3.6f), -90f, new Color(0.10f, 0.36f, 0.72f), "품질 제일");
        Poster(m, d, new Vector3(13.78f, 2.2f, 4.6f), -90f, new Color(0.90f, 0.62f, 0.05f), "끼임 주의");
    }

    static void Plant(Transform parent, Vector3 pos, float size)
    {
        var p = new GameObject("Plant").transform;
        p.SetParent(parent, false);
        p.position = pos;
        p.localScale = Vector3.one * size;
        RodY(p, "Pot", new Vector3(0, 0.17f, 0), 0.34f, 0.30f, new Color(0.72f, 0.38f, 0.26f));
        RodY(p, "PotRim", new Vector3(0, 0.34f, 0), 0.05f, 0.34f, new Color(0.66f, 0.34f, 0.22f));
        RodY(p, "Soil", new Vector3(0, 0.355f, 0), 0.02f, 0.28f, new Color(0.18f, 0.12f, 0.08f));
        Color[] greens = { new Color(0.16f, 0.46f, 0.20f), new Color(0.22f, 0.56f, 0.26f), new Color(0.12f, 0.38f, 0.18f) };
        for (int i = 0; i < 9; i++)
        {
            float ang = i * 40f;
            var leaf = Ball(p, "Leaf" + i, new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad) * 0.10f, 0.62f + (i % 3) * 0.10f, Mathf.Cos(ang * Mathf.Deg2Rad) * 0.10f), new Vector3(0.10f, 0.44f, 0.05f), greens[i % 3]);
            leaf.localRotation = Quaternion.Euler(0, ang, 0) * Quaternion.Euler(0, 0, 28f);
        }
    }

    static void Poster(Machine m, Transform parent, Vector3 pos, float yaw, Color header, string title)
    {
        var g = Group(parent, "Poster", pos, yaw);
        Box(g, "Frame", Vector3.zero, new Vector3(0.95f, 1.25f, 0.03f), new Color(0.20f, 0.21f, 0.23f));
        Box(g, "Paper", new Vector3(0, 0, 0.018f), new Vector3(0.88f, 1.18f, 0.01f), new Color(0.95f, 0.95f, 0.92f));
        Glow(Box(g, "Head", new Vector3(0, 0.38f, 0.026f), new Vector3(0.88f, 0.34f, 0.01f), header), header, 0.35f);
        for (int i = 0; i < 4; i++)
            Box(g, "Line", new Vector3(0, 0.10f - i * 0.12f, 0.026f), new Vector3(0.62f - (i % 2) * 0.12f, 0.03f, 0.008f), new Color(0.55f, 0.57f, 0.60f));
        Box(g, "Icon", new Vector3(0, -0.40f, 0.026f), new Vector3(0.22f, 0.22f, 0.008f), header);
        m.labels.Add(new WorldLabel { position = pos + new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad) * 0.03f, 0.38f, Mathf.Cos(yaw * Mathf.Deg2Rad) * 0.03f), text = title, height = 0.14f, color = Color.white });
    }

    // 품질 검사대: 모니터, 키보드, 버니어 캘리퍼스, 검사한 부품
    static void BuildQcTable(Machine m, Transform parent, Vector3 pos)
    {
        var q = new GameObject("QcTable").transform;
        q.SetParent(parent, false);
        q.position = pos;
        Skin(Box(q, "Top", new Vector3(0, 0.85f, 0), new Vector3(1.7f, 0.05f, 0.85f), new Color(0.72f, 0.74f, 0.77f)), Tx.Steel, 1f, 1f, 0.4f, 0.45f);
        foreach (float x in new[] { -0.8f, 0.8f })
            foreach (float zz in new[] { -0.38f, 0.38f })
                Box(q, "Leg", new Vector3(x, 0.42f, zz), new Vector3(0.05f, 0.84f, 0.05f), new Color(0.25f, 0.26f, 0.28f));
        Box(q, "LowerShelf", new Vector3(0, 0.25f, 0), new Vector3(1.6f, 0.03f, 0.75f), new Color(0.45f, 0.47f, 0.50f));
        // 모니터
        Box(q, "MonitorStand", new Vector3(-0.45f, 1.0f, 0.15f), new Vector3(0.05f, 0.26f, 0.05f), Dark);
        Box(q, "MonitorBody", new Vector3(-0.45f, 1.22f, 0.15f), new Vector3(0.52f, 0.34f, 0.03f), Dark);
        Glow(Box(q, "MonitorScreen", new Vector3(-0.45f, 1.22f, 0.133f), new Vector3(0.48f, 0.30f, 0.01f), new Color(0.12f, 0.40f, 0.72f)), new Color(0.15f, 0.50f, 0.90f), 0.9f);
        for (int i = 0; i < 4; i++)
            Glow(Box(q, "ScreenBar", new Vector3(-0.60f + i * 0.10f, 1.16f + 0.02f * i, 0.125f), new Vector3(0.05f, 0.06f + 0.03f * ((i * 3) % 4), 0.008f), new Color(0.80f, 0.92f, 1f)), new Color(0.80f, 0.92f, 1f), 1.1f);
        Box(q, "Keyboard", new Vector3(-0.45f, 0.885f, -0.12f), new Vector3(0.36f, 0.015f, 0.12f), new Color(0.12f, 0.12f, 0.14f));
        // 검사한 부품: 합격(검정)과 불량(빨강) 트레이
        Box(q, "TrayOk", new Vector3(0.25f, 0.88f, 0.0f), new Vector3(0.42f, 0.03f, 0.30f), new Color(0.20f, 0.45f, 0.25f));
        Box(q, "TrayNg", new Vector3(0.68f, 0.88f, 0.0f), new Vector3(0.30f, 0.03f, 0.30f), new Color(0.65f, 0.15f, 0.12f));
        for (int i = 0; i < 4; i++)
            Box(q, "PartOk", new Vector3(0.12f + i * 0.09f, 0.915f, 0.0f), new Vector3(0.07f, 0.03f, 0.10f), new Color(0.17f, 0.18f, 0.20f));
        Box(q, "PartNg", new Vector3(0.68f, 0.915f, 0.0f), new Vector3(0.07f, 0.03f, 0.10f), new Color(0.92f, 0.28f, 0.16f));
        Box(q, "Caliper", new Vector3(0.1f, 0.885f, -0.28f), new Vector3(0.22f, 0.012f, 0.03f), new Color(0.75f, 0.77f, 0.80f));
        var clip = Box(q, "Clipboard", new Vector3(0.62f, 0.882f, -0.26f), new Vector3(0.22f, 0.012f, 0.30f), new Color(0.60f, 0.45f, 0.28f));
        clip.transform.localRotation = Quaternion.Euler(0, 12f, 0);
        var paper = Box(q, "ClipPaper", new Vector3(0.62f, 0.892f, -0.26f), new Vector3(0.19f, 0.006f, 0.26f), Color.white);
        paper.transform.localRotation = Quaternion.Euler(0, 12f, 0);
        // 간판
        Box(q, "SignPost", new Vector3(0, 1.4f, 0.45f), new Vector3(0.05f, 1.1f, 0.05f), Dark);
        Glow(Box(q, "SignBoard", new Vector3(0, 2.05f, 0.45f), new Vector3(1.3f, 0.32f, 0.04f), new Color(0.10f, 0.36f, 0.72f)), new Color(0.10f, 0.36f, 0.72f), 0.6f);
        m.labels.Add(new WorldLabel { position = pos + new Vector3(0, 2.05f, 0.42f), text = "품질 검사", height = 0.20f, color = Color.white });
        MakeLight(q, "QcLamp", LightType.Point, pos + new Vector3(0, 2.7f, -0.2f), new Color(0.90f, 0.95f, 1f), 1.0f, 5.5f, LightShadows.None);
    }

    // ---------- 자료정리 문서를 공장 안 소품으로: 인수인계 화이트보드, 숙련 인력 현황 안내판 ----------

    // 인수인계 화이트보드: 문서의 예시 상황(주간조의 "특이사항 없음")을 그대로 보여 준다
    static void BuildHandoverBoard(Machine m, Transform parent, Vector3 pos)
    {
        var b = Group(parent, "HandoverBoard", pos, 180f); // 앞면이 카메라 쪽(-z)을 본다
        foreach (float x in new[] { -0.75f, 0.75f })
            Box(b, "Leg", new Vector3(x, 0.55f, 0f), new Vector3(0.05f, 1.1f, 0.05f), new Color(0.28f, 0.29f, 0.31f));
        Box(b, "Frame", new Vector3(0, 1.55f, 0f), new Vector3(1.7f, 1.05f, 0.05f), new Color(0.70f, 0.72f, 0.75f));
        Skin(Box(b, "Face", new Vector3(0, 1.55f, 0.03f), new Vector3(1.6f, 0.95f, 0.02f), new Color(0.96f, 0.96f, 0.94f)), Tx.Paint, 1f, 1f, 0f, 0.55f);
        Box(b, "Header", new Vector3(0, 1.93f, 0.045f), new Vector3(1.6f, 0.20f, 0.01f), new Color(0.10f, 0.36f, 0.72f));
        Box(b, "Tray", new Vector3(0, 1.02f, 0.06f), new Vector3(1.4f, 0.04f, 0.10f), new Color(0.60f, 0.62f, 0.65f));
        Box(b, "Marker", new Vector3(-0.3f, 1.05f, 0.09f), new Vector3(0.14f, 0.025f, 0.025f), new Color(0.15f, 0.20f, 0.60f));
        Box(b, "Sticky", new Vector3(0.45f, 1.45f, 0.045f), new Vector3(0.28f, 0.28f, 0.006f), new Color(0.98f, 0.90f, 0.35f));
        Vector3 f = pos + new Vector3(0, 0, -0.07f);
        m.labels.Add(new WorldLabel { position = f + new Vector3(0, 1.93f, 0), text = "인수인계 (주간 → 야간)", height = 0.13f, color = Color.white });
        m.labels.Add(new WorldLabel { position = f + new Vector3(-0.25f, 1.62f, 0), text = "특이사항 없음", height = 0.20f, color = new Color(0.10f, 0.14f, 0.55f) });
        m.labels.Add(new WorldLabel { position = f + new Vector3(-0.25f, 1.34f, 0), text = "설비 상태: 정상", height = 0.11f, color = new Color(0.30f, 0.32f, 0.36f) });
        m.labels.Add(new WorldLabel { position = f + new Vector3(0.45f, 1.45f, 0), text = "3호기 소리?", height = 0.07f, color = new Color(0.55f, 0.18f, 0.10f) });
    }

    // 숙련 인력 현황 안내판: 문서의 핵심 수치 한 줄씩 (출처는 자료 패널과 웹페이지에 있다)
    static void StatBoard(Machine m, Transform parent, Vector3 pos, float yaw, string big, string small, Color accent)
    {
        var g = Group(parent, "StatBoard", pos, yaw);
        Box(g, "Frame", Vector3.zero, new Vector3(1.7f, 1.05f, 0.05f), new Color(0.16f, 0.17f, 0.19f));
        Glow(Box(g, "Face", new Vector3(0, 0, 0.03f), new Vector3(1.6f, 0.95f, 0.02f), new Color(0.08f, 0.10f, 0.14f)), new Color(0.05f, 0.07f, 0.10f), 0.4f);
        Glow(Box(g, "Bar", new Vector3(0, -0.42f, 0.045f), new Vector3(1.6f, 0.05f, 0.01f), accent), accent, 0.8f);
        float sx = Mathf.Sin(yaw * Mathf.Deg2Rad), cz = Mathf.Cos(yaw * Mathf.Deg2Rad);
        Vector3 fwdv = new Vector3(sx, 0, cz) * 0.06f;
        m.labels.Add(new WorldLabel { position = pos + fwdv + Vector3.up * 0.10f, text = big, height = 0.34f, color = Color.Lerp(accent, Color.white, 0.45f) });
        m.labels.Add(new WorldLabel { position = pos + fwdv + Vector3.up * -0.20f, text = small, height = 0.095f, color = new Color(0.85f, 0.88f, 0.92f) });
    }

    static void BuildDocProps(Machine m, Transform d)
    {
        BuildHandoverBoard(m, d, new Vector3(3.6f, 0f, -5.2f));
        StatBoard(m, d, new Vector3(13.78f, 2.3f, 7.4f), -90f, "43%", "울산 제조업 근로자 중 50대 이상 (2023, 전국 38%)", new Color(0.90f, 0.30f, 0.20f));
        StatBoard(m, d, new Vector3(13.78f, 2.3f, 9.9f), -90f, "2.1배", "뿌리산업 부족 인원, 9년 사이 (2015 → 2024)", new Color(0.95f, 0.62f, 0.10f));
        StatBoard(m, d, new Vector3(13.78f, 2.3f, 12.4f), -90f, "53.4세", "대한민국명장 평균 연령", new Color(0.25f, 0.60f, 0.90f));
    }
}
