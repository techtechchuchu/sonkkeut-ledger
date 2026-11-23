using UnityEngine;

// 공장 앞쪽(관찰자 뒤쪽) 구역: 앞벽을 세우고, 그 앞을 설비로 채운다.
// 배전반, 공기압축기와 에어탱크, 냉각기(칠러), 원료 호퍼, 금형 보관 선반, 원료 팔레트.
// 흡연구역(-9, -7.2)과 휴게 구역(왼쪽 벽 쪽)은 건드리지 않는다. 모두 기본 도형이다.
public static partial class MachineBuilder
{
    static void BuildFrontZone(Machine m, Transform z)
    {
        var f = new GameObject("FrontZone").transform;
        f.SetParent(z, false);

        // 앞벽: 허공이 보이지 않게 막는다. 아래는 짙은 띠, 위에는 야간 창
        Skin(Box(f, "FrontWall", new Vector3(0, 4.5f, -9.2f), new Vector3(28.6f, 9f, 0.3f), new Color(0.84f, 0.85f, 0.86f)), Tx.Wall, 10f, 3f, 0f, 0.12f);
        Skin(Box(f, "FrontWallBand", new Vector3(0, 0.7f, -9.02f), new Vector3(28.4f, 1.4f, 0.1f), new Color(0.30f, 0.40f, 0.52f)), Tx.Wall, 10f, 1f, 0f, 0.3f);
        for (int i = 0; i < 5; i++)
            Box(f, "FrontWindow" + i, new Vector3(-9f + i * 4.5f, 6.2f, -9.02f), new Vector3(3.0f, 1.7f, 0.1f), new Color(0.07f, 0.10f, 0.20f));
        // 벽 앞 안전선
        Box(f, "FrontLane", new Vector3(0, 0.005f, -4.2f), new Vector3(20f, 0.01f, 0.12f), SafetyYellow);

        Color gray = new Color(0.55f, 0.58f, 0.62f), steel = new Color(0.62f, 0.65f, 0.68f), darkGray = new Color(0.30f, 0.33f, 0.37f);

        // 수배전반: 같은 크기의 캐비닛 다섯 개와 표시등
        for (int i = 0; i < 5; i++)
        {
            float x = -5.2f + i * 1.0f;
            Skin(Box(f, "Cabinet" + i, new Vector3(x, 1.1f, -8.75f), new Vector3(0.92f, 2.2f, 0.55f), gray), Tx.Paint, 1f, 1f, 0.2f, 0.5f);
            Box(f, "CabinetDoorLine" + i, new Vector3(x, 1.1f, -8.47f), new Vector3(0.80f, 2.0f, 0.02f), darkGray);
            Box(f, "CabinetHandle" + i, new Vector3(x + 0.32f, 1.1f, -8.44f), new Vector3(0.04f, 0.28f, 0.03f), Dark);
            Color lamp = i % 3 == 0 ? new Color(0.1f, 0.9f, 0.3f) : (i % 3 == 1 ? new Color(1f, 0.75f, 0.1f) : new Color(0.1f, 0.9f, 0.3f));
            Glow(Box(f, "CabinetLamp" + i, new Vector3(x - 0.25f, 1.75f, -8.44f), new Vector3(0.07f, 0.07f, 0.02f), lamp), lamp, 1.6f);
        }
        m.labels.Add(new WorldLabel { position = new Vector3(-3.2f, 2.65f, -8.55f), text = "수배전반", height = 0.24f, color = Color.white });
        Box(f, "WarnPlate", new Vector3(-3.2f, 2.35f, -8.44f), new Vector3(0.5f, 0.2f, 0.02f), SafetyYellow);

        // 공기압축기와 에어탱크
        Skin(Box(f, "Compressor", new Vector3(0.6f, 0.45f, -8.55f), new Vector3(1.2f, 0.9f, 0.9f), new Color(0.15f, 0.40f, 0.65f)), Tx.Paint, 1f, 1f, 0.3f, 0.5f);
        RodY(f, "CompressorMotor", new Vector3(0.35f, 1.02f, -8.55f), 0.3f, 0.5f, darkGray);
        Glow(Box(f, "CompressorGauge", new Vector3(0.9f, 0.62f, -8.08f), new Vector3(0.18f, 0.18f, 0.03f), new Color(0.95f, 0.95f, 0.92f)), new Color(0.95f, 0.95f, 0.92f), 0.8f);
        Skin(RodY(f, "AirTank", new Vector3(2.0f, 1.0f, -8.5f), 2.0f, 0.95f, new Color(0.78f, 0.80f, 0.82f)), Tx.Steel, 1f, 2f, 0.6f, 0.6f);
        Ball(f, "AirTankTop", new Vector3(2.0f, 2.0f, -8.5f), new Vector3(0.95f, 0.5f, 0.95f), new Color(0.78f, 0.80f, 0.82f));
        Box(f, "AirPipe", new Vector3(1.3f, 2.2f, -8.9f), new Vector3(1.5f, 0.12f, 0.12f), steel);
        Box(f, "AirPipeUp", new Vector3(0.6f, 1.5f, -8.9f), new Vector3(0.12f, 1.4f, 0.12f), steel);

        // 칠러(금형 냉각기) 둘: 위에 팬
        for (int i = 0; i < 2; i++)
        {
            float x = 4.2f + i * 1.9f;
            Skin(Box(f, "Chiller" + i, new Vector3(x, 0.9f, -8.35f), new Vector3(1.6f, 1.8f, 1.1f), new Color(0.88f, 0.89f, 0.90f)), Tx.Paint, 1f, 1f, 0.15f, 0.5f);
            RodY(f, "ChillerFan" + i, new Vector3(x, 1.82f, -8.35f), 0.06f, 0.9f, darkGray);
            Box(f, "ChillerLabel" + i, new Vector3(x, 1.3f, -7.79f), new Vector3(1.2f, 0.24f, 0.02f), new Color(0.15f, 0.40f, 0.65f));
            Glow(Box(f, "ChillerLamp" + i, new Vector3(x + 0.5f, 0.7f, -7.79f), new Vector3(0.08f, 0.08f, 0.02f), new Color(0.2f, 0.9f, 0.4f)), new Color(0.2f, 0.9f, 0.4f), 1.6f);
        }
        m.labels.Add(new WorldLabel { position = new Vector3(5.1f, 2.4f, -7.7f), text = "냉각수 공급", height = 0.2f, color = Color.white });
        // 칠러에서 금형 쪽으로 가는 배관
        Box(f, "CoolPipeH", new Vector3(5.0f, 2.1f, -8.9f), new Vector3(3.8f, 0.12f, 0.12f), new Color(0.15f, 0.40f, 0.65f));
        Box(f, "CoolPipeV", new Vector3(6.9f, 1.2f, -8.9f), new Vector3(0.12f, 1.9f, 0.12f), new Color(0.80f, 0.20f, 0.15f));

        // 원료 호퍼(건조기 포함): 큰 원통과 깔때기
        Vector3 hp = new Vector3(10.2f, 0f, -7.6f);
        foreach (float dx in new[] { -0.6f, 0.6f })
            foreach (float dz in new[] { -0.6f, 0.6f })
                Box(f, "HopperLeg", hp + new Vector3(dx, 0.9f, dz), new Vector3(0.10f, 1.8f, 0.10f), darkGray);
        Skin(RodY(f, "HopperBody", hp + new Vector3(0, 2.7f, 0), 1.8f, 1.5f, new Color(0.84f, 0.86f, 0.88f)), Tx.Steel, 1f, 2f, 0.5f, 0.55f);
        Ball(f, "HopperCone", hp + new Vector3(0, 1.75f, 0), new Vector3(1.5f, 0.9f, 1.5f), new Color(0.74f, 0.76f, 0.78f));
        Box(f, "HopperLadder", hp + new Vector3(0.82f, 1.8f, 0), new Vector3(0.05f, 3.4f, 0.4f), SafetyYellow);
        Box(f, "HopperPipe", hp + new Vector3(-1.2f, 3.4f, 0.4f), new Vector3(2.0f, 0.15f, 0.15f), steel);
        m.labels.Add(new WorldLabel { position = hp + new Vector3(0, 3.9f, 0.8f), text = "원료 건조 호퍼", height = 0.24f, color = Color.white });

        // 금형 보관 선반: 3단, 금형 블록
        Vector3 rk = new Vector3(12.4f, 0f, -5.4f);
        for (int s = 0; s < 4; s++)
            Box(f, "RackPost" + s, rk + new Vector3(s % 2 * 1.6f - 0.8f, 1.1f, s / 2 * 1.0f - 0.5f), new Vector3(0.08f, 2.2f, 0.08f), new Color(0.15f, 0.35f, 0.65f));
        for (int lv = 0; lv < 3; lv++)
        {
            float y = 0.35f + lv * 0.8f;
            Box(f, "RackBeam" + lv, rk + new Vector3(0, y, 0), new Vector3(1.7f, 0.06f, 1.1f), new Color(0.90f, 0.45f, 0.10f));
            Skin(Box(f, "Mold" + lv + "a", rk + new Vector3(-0.4f, y + 0.22f, 0), new Vector3(0.7f, 0.38f, 0.6f), darkGray), Tx.Steel, 1f, 1f, 0.6f, 0.45f);
            Skin(Box(f, "Mold" + lv + "b", rk + new Vector3(0.45f, y + 0.18f, 0), new Vector3(0.6f, 0.30f, 0.55f), new Color(0.38f, 0.41f, 0.45f)), Tx.Steel, 1f, 1f, 0.6f, 0.45f);
        }
        m.labels.Add(new WorldLabel { position = rk + new Vector3(-0.1f, 2.5f, -0.6f), text = "금형 보관", height = 0.22f, color = Color.white });

        // 원료 포대 팔레트 둘
        Vector3[] pal = { new Vector3(7.8f, 0f, -5.2f), new Vector3(8.9f, 0f, -5.3f) };
        for (int p = 0; p < pal.Length; p++)
        {
            Skin(Box(f, "Pallet" + p, pal[p] + new Vector3(0, 0.08f, 0), new Vector3(1.0f, 0.14f, 1.0f), new Color(0.60f, 0.46f, 0.30f)), Tx.Wood, 1f, 1f, 0f, 0.2f);
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 2; c++)
                    Skin(Box(f, "Bag" + p + r + c, pal[p] + new Vector3(-0.24f + c * 0.48f, 0.26f + r * 0.2f, 0), new Vector3(0.44f, 0.18f, 0.7f), r % 2 == 0 ? new Color(0.90f, 0.90f, 0.86f) : new Color(0.84f, 0.80f, 0.70f)), Tx.Paper, 1f, 1f, 0f, 0.3f);
        }

        // 앞쪽 천장 조명
        Color warm = new Color(1f, 0.93f, 0.80f);
        MakeLight(f, "FrontLightA", LightType.Point, new Vector3(-1f, 3.2f, -6.2f), warm, 1.1f, 9f, LightShadows.None);
        MakeLight(f, "FrontLightB", LightType.Point, new Vector3(8f, 3.2f, -6.2f), warm, 1.1f, 9f, LightShadows.None);

        // 설비 소리: 위잉 도는 모터와 펌프. 설비마다 높낮이를 달리해서 한 소리로 뭉치지 않게 한다
        FactoryAudio.AddWhir(f, new Vector3(0.6f, 0.9f, -8.4f), 1.00f, 0.55f);   // 공기압축기
        FactoryAudio.AddWhir(f, new Vector3(5.1f, 1.8f, -8.3f), 1.18f, 0.45f);   // 냉각기 팬
        FactoryAudio.AddWhir(f, new Vector3(10.2f, 2.5f, -7.6f), 0.86f, 0.40f);  // 원료 호퍼 송풍기
        FactoryAudio.AddWhir(f, new Vector3(-3.6f, 1.0f, 7.4f), 0.92f, 0.50f);   // 뒤쪽 사출기 유압 펌프
        FactoryAudio.AddWhir(f, new Vector3(3.8f, 1.0f, 7.4f), 1.08f, 0.50f);
        FactoryAudio.AddWhir(f, new Vector3(4.0f, 1.0f, 0.9f), 1.25f, 0.45f);    // 작업대 옆 칠러
        FactoryAudio.AddWhir(f, new Vector3(0f, 5.5f, 13.6f), 0.80f, 0.35f);     // 천장 컨베이어 구동 모터
    }
}
