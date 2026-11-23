using System.Collections.Generic;
using UnityEngine;

// 실제 사람 모양 작업자: 무료 CC0 캐릭터(Quaternius)에 작업복·장갑·안전화·조끼를 입히고,
// 안전모·머리카락·수염을 달고, 사람 동작(걷기·서 있기·조작·집기·수리)으로 움직인다.
// 캐릭터는 속옷만 입은 근육질 기본 몸이라서, 몸은 머리·목·손만 남기고 숨기고,
// 옷은 몸 삼각형을 복사해 "날씬하고 헐렁하게" 다시 빚어 같은 뼈대에 입힌다 (쫄티처럼 몸에 붙지 않게).
// Resources/Characters 자산이 없으면 BuildRealWorker가 null을 돌려주고, 기존 도형 작업자를 쓴다.
public static partial class MachineBuilder
{
    enum Part { None, Torso, Jacket, Pants, Glove, Boot, Skin }

    static readonly string[] FingerPrefix = { "index_", "middle_", "pinky_", "ring_", "thumb_" };

    // 뼈 이름으로 어느 부위인지
    static Part PartOf(string bone)
    {
        if (bone == "Head" || bone == "neck_01") return Part.Skin;
        if (bone.StartsWith("spine_")) return Part.Torso;
        if (bone.StartsWith("clavicle_") || bone.StartsWith("upperarm_") || bone.StartsWith("lowerarm_")) return Part.Jacket;
        if (bone == "pelvis" || bone.StartsWith("thigh_") || bone.StartsWith("calf_")) return Part.Pants;
        if (bone.StartsWith("hand_")) return Part.Glove;
        foreach (string p in FingerPrefix) if (bone.StartsWith(p)) return Part.Glove;
        if (bone.StartsWith("foot_") || bone.StartsWith("ball_")) return Part.Boot;
        return Part.None;
    }

    // 팔다리 뼈가 향하는 다음 뼈 (뼈 축을 따라 둘레를 줄이려고)
    static string ChildBone(string bone)
    {
        if (bone.StartsWith("upperarm_")) return "lowerarm_" + bone.Substring(9);
        if (bone.StartsWith("lowerarm_")) return "hand_" + bone.Substring(9);
        if (bone.StartsWith("thigh_")) return "calf_" + bone.Substring(6);
        if (bone.StartsWith("calf_")) return "foot_" + bone.Substring(5);
        return null;
    }

    // 날씬하게 만들 때 팔다리 둘레에 곱하는 값 (작을수록 가늘다)
    static float LimbSlim(string bone)
    {
        if (bone.StartsWith("upperarm_")) return 0.60f;
        if (bone.StartsWith("lowerarm_")) return 0.66f;
        if (bone.StartsWith("thigh_")) return 0.72f;
        if (bone.StartsWith("calf_")) return 0.68f;
        return 1f;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            Transform r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }

    static Material MatOf(Color c, Texture2D tex, float tile, float metal, float smooth)
    {
        var baseMat = Resources.Load<Material>("Base");
        var m = new Material(baseMat != null ? baseMat : new Material(Shader.Find("Standard")));
        m.color = c;
        if (tex != null) { m.mainTexture = tex; m.mainTextureScale = new Vector2(tile, tile); }
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        return m;
    }

    class ShellOpts
    {
        public float inflate = 0.02f;   // 법선 방향으로 부풀리는 정도(m): 클수록 헐렁
        public bool slim;               // 근육질 몸을 날씬한 체형으로 줄일지
        public int smooth = 0;          // 근육 굴곡을 펴는 평활화 횟수
        public float wrinkle = 0f;      // 옷 주름처럼 불규칙한 높낮이(m)
        public float handScale = 1f;    // 장갑 크기 (손목 기준)
    }

    // 몸 삼각형 중 pick에 해당하는 것만 복사해, (선택) 날씬하게 줄이고 헐렁하게 부풀린 껍질을 같은 뼈대에 입힌다
    static GameObject MakeShell(SkinnedMeshRenderer src, float waistY, System.Func<Part, bool> pick, ShellOpts o, Material mat, string name)
    {
        Mesh m = src.sharedMesh;
        Vector3[] v = m.vertices, n = m.normals;
        Vector2[] uv = m.uv;
        BoneWeight[] bw = m.boneWeights;
        int[] tris = m.triangles;
        Transform[] bones = src.bones;
        Transform tr = src.transform;
        var animOwner = src.GetComponentInParent<Animator>();
        Transform root = animOwner != null ? animOwner.transform : tr.root; // 이 작업자의 뼈대만 찾는다 (공장 전체에서 찾으면 첫 작업자의 뼈를 집는다)

        // 몸의 축과 방향 (세계 좌표)
        Transform pelvis = FindDeep(root, "pelvis"), neck = FindDeep(root, "neck_01");
        Vector3 axisA = pelvis != null ? pelvis.position : root.position + Vector3.up * 0.95f;
        Vector3 axisB = neck != null ? neck.position : root.position + Vector3.up * 1.5f;
        Vector3 right = root.right, fwd = root.forward, up = root.up;

        var part = new Part[v.Length];
        var dom = new int[v.Length];
        var wp = new Vector3[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            BoneWeight w = bw[i];
            int bi = w.boneIndex0; float bg = w.weight0;
            if (w.weight1 > bg) { bi = w.boneIndex1; bg = w.weight1; }
            if (w.weight2 > bg) { bi = w.boneIndex2; bg = w.weight2; }
            if (w.weight3 > bg) { bi = w.boneIndex3; bg = w.weight3; }
            dom[i] = bi;
            wp[i] = tr.TransformPoint(v[i]);
            string bn = bi >= 0 && bi < bones.Length ? bones[bi].name : "";
            part[i] = bn != "" ? PartOf(bn) : Part.None;
            // 몸통과 골반의 경계는 허리 높이에서 곧게 자른다 (뼈 가중치로 가르면 가장자리가 톱니처럼 된다)
            if (bn.StartsWith("spine_") || bn == "pelvis") part[i] = wp[i].y > waistY ? Part.Torso : Part.Pants;
        }

        // 선택된 정점만 모으고 삼각형을 다시 짠다
        var remap = new int[v.Length];
        for (int i = 0; i < remap.Length; i++) remap[i] = -1;
        var nv = new List<Vector3>(); var nuv = new List<Vector2>(); var nbw = new List<BoneWeight>(); var nt = new List<int>(); var srcIdx = new List<int>();
        for (int t = 0; t < tris.Length; t += 3)
        {
            int a = tris[t], b = tris[t + 1], c = tris[t + 2];
            int hit = (pick(part[a]) ? 1 : 0) + (pick(part[b]) ? 1 : 0) + (pick(part[c]) ? 1 : 0);
            if (hit < 2) continue; // 세 점 중 두 점 이상이 그 부위일 때만
            foreach (int idx in new[] { a, b, c })
            {
                if (remap[idx] < 0)
                {
                    remap[idx] = nv.Count;
                    nv.Add(Vector3.zero);
                    nuv.Add(idx < uv.Length ? uv[idx] : Vector2.zero);
                    nbw.Add(bw[idx]);
                    srcIdx.Add(idx);
                }
                nt.Add(remap[idx]);
            }
        }

        // 위치 만들기: 날씬하게 → 부풀리기 → (뒤에서) 평활화
        var pos = new Vector3[nv.Count];
        for (int k = 0; k < pos.Length; k++)
        {
            int i = srcIdx[k];
            Vector3 p = wp[i];
            string bn = dom[i] >= 0 && dom[i] < bones.Length ? bones[dom[i]].name : "";
            if (o.slim)
            {
                string child = ChildBone(bn);
                if (child != null)
                {
                    // 팔·다리: 뼈 축에서 멀어지는 거리를 줄여 가늘게
                    Transform cb = FindDeep(root, child);
                    if (cb != null)
                    {
                        Vector3 A = bones[dom[i]].position, B = cb.position, ab = B - A;
                        float t = Mathf.Clamp01(Vector3.Dot(p - A, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                        Vector3 q = A + ab * t;
                        p = q + (p - q) * LimbSlim(bn);
                    }
                }
                else if (part[i] == Part.Torso || bn.StartsWith("clavicle_"))
                {
                    // 몸통: 어깨 폭과 가슴 두께를 줄인다
                    float t = Mathf.Clamp01(Vector3.Dot(p - axisA, axisB - axisA) / Mathf.Max(1e-6f, (axisB - axisA).sqrMagnitude));
                    Vector3 q = axisA + (axisB - axisA) * t;
                    Vector3 d = p - q;
                    float shoulder = Mathf.Lerp(0.90f, 0.78f, t); // 어깨 쪽(위)일수록 더 좁게
                    p = q + right * (Vector3.Dot(d, right) * shoulder) + fwd * (Vector3.Dot(d, fwd) * 0.86f) + up * Vector3.Dot(d, up);
                }
                else if (part[i] == Part.Pants && bn == "pelvis")
                {
                    Vector3 q = new Vector3(axisA.x, p.y, axisA.z);
                    Vector3 d = p - q;
                    p = q + right * (Vector3.Dot(d, right) * 0.84f) + fwd * (Vector3.Dot(d, fwd) * 0.88f);
                }
            }
            if (o.handScale != 1f && part[i] == Part.Glove)
            {
                Transform hand = FindDeep(root, bn.EndsWith("_r") ? "hand_r" : "hand_l");
                if (hand != null) p = hand.position + (p - hand.position) * o.handScale;
            }
            Vector3 wn = tr.TransformDirection(n[i]).normalized;
            p += wn * o.inflate;
            if (o.wrinkle > 0f)
            {
                // 옷 주름처럼 불규칙한 높낮이 (세계 좌표 기준 값 노이즈)
                float nz = Mathf.PerlinNoise(p.x * 6.5f + 3.1f, p.y * 8.5f + p.z * 5.5f) - 0.5f;
                p += wn * nz * 2f * o.wrinkle;
            }
            pos[k] = p;
        }
        // 평활화: 이웃 정점의 평균쪽으로 당겨 근육 굴곡을 편다
        if (o.smooth > 0)
        {
            var nb = new List<int>[pos.Length];
            for (int k = 0; k < nb.Length; k++) nb[k] = new List<int>();
            for (int t = 0; t < nt.Count; t += 3)
            {
                int a = nt[t], b = nt[t + 1], c = nt[t + 2];
                nb[a].Add(b); nb[a].Add(c); nb[b].Add(a); nb[b].Add(c); nb[c].Add(a); nb[c].Add(b);
            }
            for (int it = 0; it < o.smooth; it++)
            {
                var next = new Vector3[pos.Length];
                for (int k = 0; k < pos.Length; k++)
                {
                    if (nb[k].Count == 0) { next[k] = pos[k]; continue; }
                    Vector3 avg = Vector3.zero;
                    foreach (int j in nb[k]) avg += pos[j];
                    avg /= nb[k].Count;
                    next[k] = Vector3.Lerp(pos[k], avg, 0.5f);
                }
                pos = next;
            }
        }
        for (int k = 0; k < pos.Length; k++) nv[k] = tr.InverseTransformPoint(pos[k]);

        var nm = new Mesh { name = name };
        nm.SetVertices(nv); nm.SetUVs(0, nuv);
        nm.boneWeights = nbw.ToArray();
        nm.bindposes = m.bindposes;
        nm.SetTriangles(nt, 0);
        nm.RecalculateNormals();
        nm.RecalculateBounds();

        var go = new GameObject(name);
        go.transform.SetParent(src.transform.parent, false);
        go.transform.localPosition = src.transform.localPosition;
        go.transform.localRotation = src.transform.localRotation;
        go.transform.localScale = src.transform.localScale;
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = nm;
        smr.bones = bones;
        smr.rootBone = src.rootBone;
        smr.sharedMaterial = mat;
        smr.localBounds = src.localBounds;
        return go;
    }

    static WorkerRig BuildRealWorker(Machine m, Transform parent, Vector3 pos, float yaw, WorkerMode mode, Color vest, Color helmetColor)
    {
        var prefab = Resources.Load<GameObject>("Characters/WorkerBody");
        var ctrl = Resources.Load<RuntimeAnimatorController>("Characters/Worker");
        if (prefab == null || ctrl == null) return null;

        int idx = m.workers.Count;
        var rig = new WorkerRig { mode = mode, phase = idx * 1.7f };
        var go = Object.Instantiate(prefab, parent);
        go.name = "Worker";
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        rig.root = go.transform;

        var anim = go.GetComponent<Animator>();
        if (anim == null) anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        rig.animator = anim;

        // 부위 찾기
        SkinnedMeshRenderer body = null, eyes = null, brows = null;
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.name.Contains("Eyebrows")) brows = smr;
            else if (smr.name.Contains("Eyes")) eyes = smr;
            else body = smr;
        }
        if (body == null || eyes == null) { Object.Destroy(go); return null; }

        // 사람마다 다른 피부·머리색·키
        bool darkSkin = idx % 4 == 3 || idx % 4 == 1;
        Color tint = new[] { new Color(1f, 1f, 1f), new Color(0.96f, 0.90f, 0.86f), new Color(1.0f, 0.97f, 0.95f), new Color(0.92f, 0.86f, 0.82f) }[idx % 4];
        Color hairColor = FaceStyles[idx % FaceStyles.Length].hair;
        Material skin = new Material(Resources.Load<Material>(darkSkin ? "Characters/M_Skin_Dark" : "Characters/M_Skin_Light"));
        skin.color = tint;
        body.sharedMaterial = skin;
        eyes.sharedMaterial = Resources.Load<Material>("Characters/M_Eye");
        if (brows != null) brows.sharedMaterial = MatOf(hairColor, null, 1, 0, 0.2f);

        // 눈높이를 약 1.62m(사람마다 조금씩 다르게)로 맞춘다
        float tall = WorkerHeights[idx % WorkerHeights.Length];
        float eyeH = Mathf.Max(0.5f, eyes.bounds.center.y - go.transform.position.y);
        go.transform.localScale = Vector3.one * (1.62f * tall / eyeH);

        rig.head = FindDeep(go.transform, "Head");
        rig.eyeRenderer = eyes;
        rig.basePos = pos;
        rig.baseYaw = yaw;

        // 옷과 장갑·안전화는 몸을 날씬하게 줄인 뒤 헐렁하게 입힌다. 원래 몸은 머리·목·손만 남기고 숨긴다
        Color[] jackets = { new Color(0.30f, 0.37f, 0.48f), new Color(0.27f, 0.35f, 0.31f), new Color(0.38f, 0.40f, 0.44f), new Color(0.25f, 0.31f, 0.43f) };
        Color pants = new Color(0.14f, 0.19f, 0.30f);
        var fabric = Tx.Fabric;
        Transform spine1 = FindDeep(go.transform, "spine_01");
        float waistY = spine1 != null ? spine1.position.y - 0.02f : go.transform.position.y + 0.95f;

        MakeShell(body, waistY, p => p == Part.Torso || p == Part.Jacket, new ShellOpts { slim = true, inflate = 0.030f, smooth = 3, wrinkle = 0.007f }, MatOf(jackets[idx % jackets.Length], fabric, 6f, 0f, 0.05f), "Jacket");
        MakeShell(body, waistY, p => p == Part.Pants, new ShellOpts { slim = true, inflate = 0.028f, smooth = 3, wrinkle = 0.007f }, MatOf(pants, fabric, 6f, 0f, 0.05f), "Pants");
        MakeShell(body, waistY, p => p == Part.Glove, new ShellOpts { inflate = 0.010f, handScale = 0.86f }, MatOf(new Color(0.94f, 0.94f, 0.90f), null, 1, 0f, 0.2f), "Gloves");
        MakeShell(body, waistY, p => p == Part.Boot, new ShellOpts { inflate = 0.030f, smooth = 1 }, MatOf(new Color(0.10f, 0.09f, 0.09f), null, 1, 0f, 0.3f), "Boots");
        MakeShell(body, waistY, p => p == Part.Torso, new ShellOpts { slim = true, inflate = 0.052f, smooth = 4, wrinkle = 0.004f }, MatOf(vest, null, 1, 0.1f, 0.35f), "Vest");
        // 보이는 피부는 머리와 목뿐이다 (몸 전체는 옷 속이라 숨긴다)
        MakeShell(body, waistY, p => p == Part.Skin, new ShellOpts { inflate = 0f }, skin, "Skin");
        body.enabled = false;
        // 어깨가 근육질로 넓지 않게, 어깨뼈를 몸 쪽으로 모은다 (팔 길이는 그대로)
        foreach (string cn in new[] { "clavicle_l", "clavicle_r" })
        {
            Transform cl = FindDeep(go.transform, cn);
            if (cl == null) continue;
            Vector3 center = new Vector3(spine1 != null ? spine1.position.x : cl.position.x, cl.position.y, spine1 != null ? spine1.position.z : cl.position.z);
            cl.position = Vector3.Lerp(cl.position, center, 0.16f);
        }

        // 안전모: 눈 위치를 기준으로 머리뼈에 붙인다 (세계 좌표를 유지한 채 머리뼈의 자식으로)
        Vector3 ec = eyes.bounds.center;
        Vector3 up = go.transform.up, fwd = go.transform.forward;
        float s = go.transform.localScale.x;
        var helmet = Ball(go.transform, "Helmet", Vector3.zero, new Vector3(0.222f, 0.190f, 0.256f) * s, helmetColor);
        helmet.position = ec + up * (0.088f * s) - fwd * (0.010f * s);
        helmet.rotation = go.transform.rotation;
        var brim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(brim.GetComponent<Collider>());
        brim.name = "HelmetBrim";
        brim.transform.SetParent(go.transform, false);
        brim.transform.position = ec + up * (0.050f * s) + fwd * (0.048f * s);
        brim.transform.rotation = go.transform.rotation;
        brim.transform.localScale = new Vector3(0.27f, 0.008f, 0.29f);
        Paint(brim, helmetColor);
        if (rig.head != null)
        {
            helmet.SetParent(rig.head, true);
            brim.transform.SetParent(rig.head, true);
        }

        // 머리카락과 수염 (몸과 같은 좌표에서 만들어져 있어, 몸 옆에 놓았다가 머리뼈에 붙이면 제자리에 앉는다)
        string hairName = idx % 4 == 2 ? "Hair_SimpleParted" : "Hair_Buzzed";
        AttachHair(go, rig.head, hairName, hairColor);
        if (FaceStyles[idx % FaceStyles.Length].stubble >= 0.5f) AttachHair(go, rig.head, "Hair_Beard", hairColor);

        if (mode == WorkerMode.Fix)
        {
            Transform hand = FindDeep(go.transform, "hand_r");
            if (hand != null)
            {
                rig.hammer = MakeTool(hand, true);
                rig.wrench = MakeTool(hand, false);
                rig.hammer.SetActive(false);
                rig.wrench.SetActive(false);
            }
        }

        m.workers.Add(rig);
        return rig;
    }

    // 손에 쥐는 공구: 부모 크기와 상관없이 실제 크기(m)가 되도록 보정한다. 손뼈의 +X 쪽이 손가락 방향
    static GameObject MakeTool(Transform hand, bool hammer)
    {
        var t = new GameObject(hammer ? "Drill" : "Wrench").transform;
        t.SetParent(hand, false);
        float k = 1f / Mathf.Max(0.0001f, hand.lossyScale.x);
        t.localScale = Vector3.one * k;
        t.localPosition = new Vector3(0.02f * k, 0f, 0f);
        Color wood = new Color(0.45f, 0.30f, 0.16f), steel = new Color(0.55f, 0.57f, 0.60f);
        if (hammer)
        {
            // 전동 드릴: 몸통(주황) + 손잡이 + 척 + 드릴 날
            Color body = new Color(0.95f, 0.50f, 0.08f), dark = new Color(0.12f, 0.12f, 0.13f);
            Paint(Box(t, "Body", new Vector3(0.10f, 0, 0), new Vector3(0.17f, 0.055f, 0.055f), body).gameObject, body);
            Paint(Box(t, "Grip", new Vector3(0.07f, -0.06f, 0), new Vector3(0.04f, 0.10f, 0.045f), dark).gameObject, dark);
            Paint(Box(t, "Chuck", new Vector3(0.205f, 0, 0), new Vector3(0.04f, 0.035f, 0.035f), steel).gameObject, steel);
            Paint(Box(t, "Bit", new Vector3(0.26f, 0, 0), new Vector3(0.07f, 0.010f, 0.010f), steel).gameObject, steel);
        }
        else
        {
            Paint(Box(t, "Shaft", new Vector3(0.10f, 0, 0), new Vector3(0.28f, 0.03f, 0.014f), steel).gameObject, steel);
            Paint(Box(t, "Jaw", new Vector3(0.26f, 0, 0), new Vector3(0.05f, 0.06f, 0.014f), steel).gameObject, steel);
        }
        foreach (var c in t.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        return t.gameObject;
    }

    static void AttachHair(GameObject character, Transform head, string prefabName, Color color)
    {
        var prefab = Resources.Load<GameObject>("Characters/" + prefabName);
        if (prefab == null || head == null) return;
        var h = Object.Instantiate(prefab, character.transform);
        h.name = prefabName;
        h.transform.localPosition = Vector3.zero;
        h.transform.localRotation = Quaternion.identity;
        h.transform.localScale = Vector3.one;
        var mat = MatOf(color, null, 1, 0f, 0.25f);
        foreach (var r in h.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
        h.transform.SetParent(head, true);
    }
}
