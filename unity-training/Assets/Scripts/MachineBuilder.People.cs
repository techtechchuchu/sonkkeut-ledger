using UnityEngine;

// 사람 얼굴: 기본 도형만으로는 사진 같은 얼굴을 만들 수 없어서, 턱선이 있는 머리 모양을 코드로 만들고
// 그 위에 눈(흰자·홍채·동공·속눈썹)·눈썹·코·입술·보안경·수염·주름을 그린 얼굴 질감을 입힌다. 사람마다 다르게 그린다.
public static partial class MachineBuilder
{
    class FaceStyle
    {
        public Color skin, hair, iris;
        public float browThick = 1f;   // 눈썹 굵기
        public float stubble;          // 수염 자국 (0~1)
        public float age;              // 주름 (0~1)
        public bool glasses;           // 보안경 테를 그린다
    }

    static readonly FaceStyle[] FaceStyles =
    {
        new FaceStyle { skin = new Color(0.88f, 0.72f, 0.60f), hair = new Color(0.07f, 0.06f, 0.06f), iris = new Color(0.22f, 0.13f, 0.07f), browThick = 1.1f, stubble = 0f,    age = 0f,    glasses = true },
        new FaceStyle { skin = new Color(0.80f, 0.62f, 0.48f), hair = new Color(0.15f, 0.11f, 0.09f), iris = new Color(0.18f, 0.11f, 0.06f), browThick = 1.4f, stubble = 0.55f, age = 0.2f,  glasses = false },
        new FaceStyle { skin = new Color(0.90f, 0.76f, 0.65f), hair = new Color(0.30f, 0.28f, 0.27f), iris = new Color(0.30f, 0.20f, 0.10f), browThick = 1.0f, stubble = 0.15f, age = 0.85f, glasses = true },
        new FaceStyle { skin = new Color(0.74f, 0.56f, 0.44f), hair = new Color(0.09f, 0.08f, 0.07f), iris = new Color(0.20f, 0.12f, 0.07f), browThick = 1.25f, stubble = 0.7f, age = 0.35f, glasses = false },
    };

    static Texture2D[] faceTex = new Texture2D[4];
    static Mesh headMesh;

    static float Soft(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    // 타원 마스크 (가운데 1, 가장자리에서 부드럽게 0)
    static float El(float u, float v, float cx, float cy, float rx, float ry)
    {
        float d = Mathf.Sqrt(((u - cx) / rx) * ((u - cx) / rx) + ((v - cy) / ry) * ((v - cy) / ry));
        return 1f - Soft(0.72f, 1f, d);
    }

    static Color Mix(Color a, Color b, float t) { return Color.Lerp(a, b, Mathf.Clamp01(t)); }

    // u: 0~1(왼→오), v: 0~1(아래→위). 얼굴은 가운데에 그려지고 가장자리는 피부/머리색으로 이어진다
    static Color FacePixel(float u, float v, FaceStyle s, int seed)
    {
        // 피부: 이마는 밝고 턱은 약간 어둡다, 미세한 얼룩
        float pore = (Fbm(u, v, 24, 70 + seed) - 0.5f) * 0.05f + (H((int)(u * 256), (int)(v * 256), 90 + seed) - 0.5f) * 0.025f;
        Color c = s.skin * (1f + (v - 0.5f) * 0.12f + pore);
        c.a = 1f;
        Color blush = new Color(0.86f, 0.46f, 0.40f);
        c = Mix(c, blush, 0.22f * (El(u, v, 0.27f, 0.45f, 0.13f, 0.08f) + El(u, v, 0.73f, 0.45f, 0.13f, 0.08f)));

        // 수염 자국: 코 아래부터 턱까지 점으로
        if (s.stubble > 0f)
        {
            float zone = Soft(0.46f, 0.30f, v) * (1f - El(u, v, 0.5f, 0.30f, 0.07f, 0.022f) * 0.8f) * Soft(0.10f, 0.22f, u) * Soft(0.10f, 0.22f, 1f - u);
            float dot = H((int)(u * 256), (int)(v * 256), 130 + seed);
            if (dot < s.stubble * 0.55f * zone) c = Mix(c, s.hair * 0.8f, 0.55f);
            c = Mix(c, s.hair, zone * s.stubble * 0.12f);
        }

        // 주름: 팔자 주름, 이마 주름, 눈꼬리
        if (s.age > 0f)
        {
            foreach (float sx in new[] { -1f, 1f })
            {
                float fold = El(u, v, 0.5f + sx * 0.115f, 0.36f, 0.018f, 0.075f);
                c = Mix(c, s.skin * 0.62f, fold * 0.45f * s.age);
                float crow = El(u, v, 0.5f + sx * 0.265f, 0.565f, 0.03f, 0.014f);
                c = Mix(c, s.skin * 0.7f, crow * 0.35f * s.age);
            }
            for (int k = 0; k < 3; k++)
                c = Mix(c, s.skin * 0.78f, El(u, v, 0.5f, 0.735f + k * 0.03f, 0.2f, 0.004f) * 0.35f * s.age);
        }

        // 눈 주변 그늘 (눈두덩)
        foreach (float sx in new[] { -1f, 1f })
        {
            float cx = 0.5f + sx * 0.155f, cy = 0.57f;
            c = Mix(c, s.skin * 0.62f, El(u, v, cx, cy + 0.012f, 0.115f, 0.06f) * 0.22f);
            // 흰자
            float white = El(u, v, cx, cy, 0.066f, 0.027f);
            c = Mix(c, new Color(0.94f, 0.93f, 0.91f), white);
            // 홍채와 동공 (위 눈꺼풀에 가려 위쪽이 살짝 잘린다)
            float lidCut = Soft(cy + 0.019f, cy + 0.010f, v);
            float iris = El(u, v, cx, cy - 0.002f, 0.031f, 0.026f) * lidCut;
            c = Mix(c, s.iris, iris);
            c = Mix(c, s.iris * 1.6f + new Color(0.05f, 0.03f, 0.01f), El(u, v, cx, cy - 0.006f, 0.019f, 0.015f) * lidCut * 0.45f * iris);
            c = Mix(c, new Color(0.02f, 0.02f, 0.02f), El(u, v, cx, cy - 0.002f, 0.014f, 0.012f) * lidCut);
            c = Mix(c, Color.white, El(u, v, cx - 0.010f * sx, cy + 0.006f, 0.006f, 0.005f) * 0.9f); // 눈의 반사광
            // 위 눈꺼풀 선과 속눈썹, 아래 눈꺼풀
            c = Mix(c, s.hair * 0.5f, El(u, v, cx, cy + 0.022f, 0.074f, 0.0085f) * 0.9f);
            c = Mix(c, s.skin * 0.55f, El(u, v, cx, cy + 0.036f, 0.07f, 0.005f) * 0.45f); // 쌍꺼풀 선
            c = Mix(c, s.skin * 0.55f, El(u, v, cx, cy - 0.027f, 0.058f, 0.0045f) * 0.35f);

            // 눈썹: 바깥쪽이 가늘어지는 호
            float t = (u - (0.5f + sx * 0.165f)) / 0.105f;
            if (Mathf.Abs(t) < 1f)
            {
                float by = 0.655f + 0.016f * (1f - t * t) + sx * -t * 0.004f;
                float thick = 0.020f * s.browThick * (1f - 0.55f * Mathf.Abs(t));
                float m = 1f - Soft(thick * 0.5f, thick, Mathf.Abs(v - by));
                float hairy = 0.75f + 0.25f * H((int)(u * 256), (int)(v * 256), 150 + seed);
                c = Mix(c, s.hair * hairy, m * 0.92f);
            }
        }

        // 코: 콧대 양옆 그늘, 코끝 하이라이트, 콧구멍, 코 밑 그림자
        foreach (float sx in new[] { -1f, 1f })
        {
            c = Mix(c, s.skin * 0.7f, El(u, v, 0.5f + sx * 0.058f, 0.50f, 0.012f, 0.085f) * 0.18f);
            c = Mix(c, new Color(0.18f, 0.08f, 0.06f), El(u, v, 0.5f + sx * 0.034f, 0.402f, 0.013f, 0.0075f) * 0.7f);
            c = Mix(c, s.skin * 0.72f, El(u, v, 0.5f + sx * 0.045f, 0.418f, 0.020f, 0.016f) * 0.22f);
        }
        c = Mix(c, s.skin * 1.12f, El(u, v, 0.5f, 0.435f, 0.032f, 0.022f) * 0.35f);
        c = Mix(c, s.skin * 1.1f, El(u, v, 0.5f, 0.54f, 0.018f, 0.06f) * 0.2f);
        c = Mix(c, s.skin * 0.6f, El(u, v, 0.5f, 0.388f, 0.055f, 0.011f) * 0.22f);

        // 입: 윗입술은 어둡고 아랫입술은 밝고 도톰하게, 가운데 입선
        Color lipTone = Mix(s.skin, new Color(0.70f, 0.30f, 0.28f), 0.5f);
        c = Mix(c, lipTone * 0.92f, El(u, v, 0.5f, 0.318f, 0.072f, 0.0135f) * 0.9f);
        c = Mix(c, lipTone * 1.08f, El(u, v, 0.5f, 0.288f, 0.066f, 0.0165f) * 0.9f);
        c = Mix(c, new Color(0.22f, 0.07f, 0.07f), El(u, v, 0.5f, 0.302f, 0.074f, 0.0033f) * 0.9f);
        c = Mix(c, s.skin * 0.8f, El(u, v, 0.5f, 0.258f, 0.045f, 0.011f) * 0.18f); // 아랫입술 밑 그늘

        // 보안경 테: 각 눈을 두른 얇은 테와 콧등 다리, 귀로 가는 다리
        if (s.glasses)
        {
            Color frame = new Color(0.10f, 0.12f, 0.15f);
            foreach (float sx in new[] { -1f, 1f })
            {
                float cx = 0.5f + sx * 0.155f, cy = 0.568f;
                float d = Mathf.Sqrt(((u - cx) / 0.098f) * ((u - cx) / 0.098f) + ((v - cy) / 0.060f) * ((v - cy) / 0.060f));
                c = Mix(c, frame, (1f - Soft(0.045f, 0.075f, Mathf.Abs(d - 1f))) * 0.95f);
                c = Mix(c, new Color(0.75f, 0.85f, 0.92f), El(u, v, cx, cy, 0.092f, 0.056f) * 0.07f); // 렌즈의 옅은 반사
            }
            c = Mix(c, frame, (1f - Soft(0.0f, 0.007f, Mathf.Abs(v - 0.585f))) * (1f - Soft(0.045f, 0.06f, Mathf.Abs(u - 0.5f))) * 0.9f);
            c = Mix(c, frame, (1f - Soft(0.0f, 0.006f, Mathf.Abs(v - 0.585f))) * Soft(0.31f, 0.34f, Mathf.Abs(u - 0.5f)) * 0.85f);
        }

        // 머리선과 구레나룻, 옆면과 뒤는 머리색/피부색으로 (뒤통수는 머리카락이 덮는다)
        float hairline = 0.835f - 0.025f * Mathf.Sin((u - 0.5f) * Mathf.PI * 1.3f) - 0.03f * (1f - Soft(0.0f, 0.4f, Mathf.Abs(u - 0.5f)));
        c = Mix(c, s.hair, Soft(hairline - 0.012f, hairline + 0.012f, v));
        float side = Soft(0.11f, 0.04f, u) + Soft(0.89f, 0.96f, u);   // 구레나룻은 귀 위쪽 가장자리만
        c = Mix(c, s.hair, Mathf.Clamp01(side) * Soft(0.52f, 0.64f, v) * 0.9f);
        if (u < 0.03f || u > 0.97f) c = v > 0.45f ? s.hair : s.skin;
        c.a = 1f;
        return c;
    }

    static Texture2D FaceTexture(int variant)
    {
        variant = ((variant % 4) + 4) % 4;
        if (faceTex[variant] != null) return faceTex[variant];
        FaceStyle s = FaceStyles[variant];
        const int n = 256;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true);
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = FacePixel((x + 0.5f) / n, (y + 0.5f) / n, s, variant * 11);
        t.SetPixels(px);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        t.anisoLevel = 4;
        t.Apply(true);
        faceTex[variant] = t;
        return t;
    }

    // 턱이 좁아지고 앞으로 살짝 나온 머리 모양. 앞에서 곧게 투영한 UV로 얼굴 질감을 입힌다
    static Mesh HeadMesh()
    {
        if (headMesh != null) return headMesh;
        const int lon = 32, lat = 22;
        const float rx = 0.100f, ry = 0.118f, rz = 0.108f;
        var verts = new Vector3[(lon + 1) * (lat + 1)];
        var uvs = new Vector2[verts.Length];
        var normals = new Vector3[verts.Length];
        for (int i = 0; i <= lat; i++)
        {
            float a = Mathf.PI * i / lat;      // 0 정수리 ~ π 턱 아래
            float y = Mathf.Cos(a), r = Mathf.Sin(a);
            float t = Mathf.Clamp01(-y);       // 아래쪽일수록 1
            for (int j = 0; j <= lon; j++)
            {
                float b = 2f * Mathf.PI * j / lon;   // 0 = 앞(+z)
                float x = Mathf.Sin(b) * r, z = Mathf.Cos(b) * r;
                float px = x * rx * (1f - 0.22f * t * t);
                float pz = z * rz * (1f - 0.10f * t) + (z > 0f ? 0.010f * t * t : 0f);
                float py = y * ry;
                int k = i * (lon + 1) + j;
                verts[k] = new Vector3(px, py, pz);
                normals[k] = new Vector3(px / (rx * rx), py / (ry * ry), pz / (rz * rz)).normalized;
                float u = pz >= 0f ? 0.5f + px / (2f * rx) : (px < 0f ? 0f : 1f); // 뒤쪽은 가장자리 열(피부/머리색)을 쓴다
                uvs[k] = new Vector2(Mathf.Clamp01(u), 0.5f + py / (2f * ry));
            }
        }
        var tris = new int[lon * lat * 6];
        int q = 0;
        for (int i = 0; i < lat; i++)
            for (int j = 0; j < lon; j++)
            {
                int a = i * (lon + 1) + j, b = a + lon + 1;
                AddTri(tris, ref q, verts, normals, a, b, a + 1);
                AddTri(tris, ref q, verts, normals, a + 1, b, b + 1);
            }
        headMesh = new Mesh { name = "Head" };
        headMesh.vertices = verts;
        headMesh.uv = uvs;
        headMesh.normals = normals;
        headMesh.triangles = tris;
        headMesh.RecalculateBounds();
        return headMesh;
    }

    // 법선 쪽을 보는 감김 방향으로 삼각형을 넣는다
    static void AddTri(int[] tris, ref int q, Vector3[] v, Vector3[] n, int a, int b, int c)
    {
        Vector3 geo = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        Vector3 avg = n[a] + n[b] + n[c];
        if (Vector3.Dot(geo, avg) < 0f) { int t = b; b = c; c = t; }
        tris[q++] = a; tris[q++] = b; tris[q++] = c;
    }

    // 머리(얼굴) 오브젝트를 만든다
    static Transform MakeFace(Transform head, int variant)
    {
        var go = new GameObject("Face");
        go.transform.SetParent(head, false);
        go.AddComponent<MeshFilter>().sharedMesh = HeadMesh();
        go.AddComponent<MeshRenderer>();
        Skin(Paint(go, Color.white), FaceTexture(variant), 1f, 1f, 0f, 0.28f);
        return go.transform;
    }
}
