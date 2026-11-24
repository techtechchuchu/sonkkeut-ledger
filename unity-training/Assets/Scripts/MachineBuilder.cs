using System.Collections.Generic;
using UnityEngine;

public enum WorkerMode { Operate, Assemble, Walk, Fix }

// 작업자 한 명의 회전축 모음
public class WorkerRig
{
    public Transform root, shoulderL, shoulderR, hipL, hipR, head;
    public GameObject hammer, wrench; // 정비 작업자가 손에 드는 공구
    public float fixCycleAt;         // 정비 동작 순환(무릎 꿇고 렌치 → 서서 망치)의 시작 시각
    public Transform chest;          // 숨쉬기·몸 기울임용 상체 축
    public Transform elbowL, elbowR, kneeL, kneeR; // 팔꿈치·무릎 관절
    public Animator animator;        // 실제 사람 캐릭터일 때 (없으면 도형 작업자)
    public string curState;          // 지금 재생 중인 동작 이름
    public Renderer eyeRenderer;     // 1인칭 눈 위치 (실제 캐릭터)
    public string say;               // 지금 말풍선에 보이는 말
    public float sayStart, sayUntil; // 말을 시작한 시각, 말풍선이 사라질 시각
    public int lineNo;               // 다음에 할 말의 번호
    public bool wasTalking;          // 직전 프레임에 이야기 동작 중이었는지
    public float pauseUntil;         // 걷다가 잠시 멈춰 서 있는 시각(Time.time)
    public Transform[] eyes;         // 눈 깜빡임용
    public Vector3 basePos;          // 서 있는 자리 (몸무게 이동의 기준)
    public float baseYaw;
    public WorkerMode mode;
    public float phase;
    public float walkMinX, walkMaxX, walkDir = 1f;
    public float walkSpeed;          // 지금 걷는 속도(m/s). 천천히 붙고 천천히 줄어든다
    public float animSpeed = 1f;     // 사람마다 조금 다른 동작 속도 (모두 똑같이 움직이지 않게)
    public bool yawInit;
}

// 3D 표지판 위에 그릴 글자. 카메라가 고정이라 화면 좌표로 투영해 그린다
public class WorldLabel
{
    public Vector3 position;
    public string text;
    public float height;   // 글자 높이(m)
    public Color color;
    public bool isSelf;    // 주인공 자신의 이름표 (1인칭에서는 숨긴다)
}

// 코드로 만든 공장 장면에서 움직이거나 색이 바뀌는 부품들
public class Machine
{
    public Transform movingPlaten;                 // 훈련 대상 사출기의 가동 플래튼 (localPosition.x로 움직인다)
    public const float ClosedX = -1.405f;
    public const float OpenX = -2.0f;
    public Renderer[] lamps;                       // 노하우 5개의 상태등
    public Transform partsRoot;
    public WorkerRig self;                         // 주인공(김민준) 작업자. 1인칭 시점의 눈 위치
    public Camera cam;                             // 장면 카메라 (아주 작게 흔들어 실제 촬영처럼 보이게 한다)
    public Vector3 camBase, camTarget;
    public Renderer beacon, screen;                // 경광등(깜빡임), 조작반 화면(발광)
    public Vector3 partOrigin = new Vector3(-0.95f, 1.35f, -0.2f);
    public readonly List<Transform> backgroundPlatens = new List<Transform>(); // 뒤쪽에서 돌아가는 사출기들
    public readonly List<Transform> hangingDoors = new List<Transform>();      // 천장 컨베이어에 매달려 이동하는 도어
    public readonly List<Renderer> doorTrims = new List<Renderer>();           // 각 도어에 붙는 트림 (작업 구간을 지나면 보인다)
    public readonly List<WorldLabel> labels = new List<WorldLabel>();          // 표지판 글자 (화면 위에 겹쳐 그린다)
    public readonly List<WorkerRig> workers = new List<WorkerRig>();           // 동작을 주는 작업자들
    public const float DoorLineStart = -10f;
    public const float DoorLineEnd = 10f;
    public const float TrimStationX = 0.8f;

    // 생산품이 놓이는 작업대 위 자리 (20열 × 5행)
    public Vector3 Slot(int i)
    {
        int col = i % 20, row = i / 20;
        return new Vector3(-2.65f + col * 0.15f, 0.63f, -1.25f - row * 0.2f);
    }
}

// 기본 도형만으로 공장 건물·사출기·작업자·조명·카메라를 만든다 (외부 에셋 없음)
public static partial class MachineBuilder
{
    static readonly Color Body = new Color(0.20f, 0.33f, 0.47f);
    static readonly Color Steel = new Color(0.62f, 0.65f, 0.69f);
    static readonly Color Dark = new Color(0.16f, 0.17f, 0.20f);
    static readonly Color Mold = new Color(0.45f, 0.47f, 0.52f);
    static readonly Color SafetyYellow = new Color(0.96f, 0.76f, 0.08f);
    public static readonly Color LampOff = new Color(0.35f, 0.36f, 0.38f);
    public static readonly Color LampWait = new Color(0.98f, 0.70f, 0.10f);
    public static readonly Color LampOn = new Color(0.05f, 0.64f, 0.05f);

    static Material baseMat;

    public static Machine Build()
    {
        var m = new Machine();
        BuildLightAndCamera(m);
        BuildHall();

        // 훈련 대상 사출기 (원점)
        var main = new GameObject("InjectionMachine_Main").transform;
        m.movingPlaten = BuildMachine(main, Body);
        m.beacon = lastBeacon;
        BuildConsole(main, m);

        // 뒤쪽 라인의 사출기들
        Vector3[] spots = { new Vector3(-3.6f, 0, 7.4f), new Vector3(3.8f, 0, 7.4f) };
        Color[] bodies = { new Color(0.22f, 0.42f, 0.36f), Body };
        for (int i = 0; i < spots.Length; i++)
        {
            var t = new GameObject("InjectionMachine_" + i).transform;
            m.backgroundPlatens.Add(BuildMachine(t, bodies[i]));
            t.position = spots[i];
        }

        // 생산품 작업대, 원료, 부대설비, 작업자
        var props = new GameObject("Props").transform;
        Box(props, "Bench", new Vector3(-1.22f, 0.3f, -1.65f), new Vector3(3.3f, 0.6f, 1.15f), new Color(0.78f, 0.74f, 0.66f));
        Pallet(props, new Vector3(-4.7f, 0, -0.4f));
        Pallet(props, new Vector3(-4.9f, 0, 3.2f));
        Box(props, "Chiller", new Vector3(4.0f, 0.75f, 0.9f), new Vector3(0.9f, 1.5f, 0.9f), new Color(0.82f, 0.84f, 0.86f));
        Box(props, "ChillerPanel", new Vector3(4.0f, 1.2f, 0.44f), new Vector3(0.5f, 0.3f, 0.02f), Dark);
        Rod(props, "Hose", new Vector3(3.3f, 0.5f, 0.6f), 1.2f, 0.07f, new Color(0.15f, 0.35f, 0.75f));
        for (int i = 0; i < 3; i++)
        {
            var drum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            drum.name = "Drum" + i;
            drum.transform.SetParent(props, false);
            drum.transform.localPosition = new Vector3(5.2f + (i % 2) * 0.7f, 0.45f, -0.9f + (i / 2) * 0.7f);
            drum.transform.localScale = new Vector3(0.6f, 0.45f, 0.6f);
            Paint(drum, new Color(0.14f, 0.36f, 0.66f));
        }
        Color orangeVest = new Color(0.96f, 0.45f, 0.06f);
        Color limeVest = new Color(0.72f, 0.86f, 0.10f);
        // 조작반 앞의 신입 작업자
        m.self = BuildWorker(m, props, new Vector3(1.75f, 0, -1.85f), -28f, WorkerMode.Operate, orangeVest, SafetyYellow);
        // 통로를 오가며 설비를 살피는 작업자
        WorkerRig walker = BuildWorker(m, props, new Vector3(-4f, 0, 4.3f), 90f, WorkerMode.Walk, limeVest, Color.white);
        walker.walkMinX = -5.5f; walker.walkMaxX = 5.5f;

        if (Resources.Load<GameObject>("Characters/WorkerBody") != null)
            BuildWorker(m, props, new Vector3(-3.0f, 0f, 1.4f), 100f, WorkerMode.Fix, new Color(0.72f, 0.86f, 0.10f), Color.white); // 무릎 꿇고 기계를 고치는 작업자

        BuildDoorLine(m);
        BuildSigns(m);
        BuildDetails(m); // 조명 기구, 배관, 난간, 선반, 공구함 등 (MachineBuilder.Realism.cs)

        m.partsRoot = new GameObject("Parts").transform;
        return m;
    }

    // 야간 교대: 낮은 주변광과 차가운 보조광 위에, 천장 형광등 아래만 환하게 밝힌다 (조명 기구는 Realism.cs)
    static void BuildLightAndCamera(Machine m)
    {
        var lightGo = new GameObject("Sun"); // 지붕 틈으로 들어오는 약한 보조광 (실내 바운스 느낌)
        var sun = lightGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.55f;
        sun.color = new Color(0.80f, 0.86f, 1.00f);
        sun.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.36f, 0.40f, 0.48f);
        RenderSettings.ambientEquatorColor = new Color(0.28f, 0.30f, 0.35f);
        RenderSettings.ambientGroundColor = new Color(0.15f, 0.16f, 0.18f);
        // 멀리 갈수록 살짝 뿌옇게 (먼지 낀 공장 공기)
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.010f;
        RenderSettings.fogColor = new Color(0.12f, 0.14f, 0.18f);
        QualitySettings.shadowDistance = 45f;
        QualitySettings.pixelLightCount = 6;

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
        cam.fieldOfView = 42f;
        cam.farClipPlane = 90f;
        camGo.transform.position = new Vector3(-0.2f, 5.8f, -11.2f);
        camGo.transform.LookAt(new Vector3(-0.2f, 1.25f, 0.8f));
        m.cam = cam;
        m.camBase = camGo.transform.position;
        m.camTarget = new Vector3(-0.2f, 1.25f, 0.8f);
        camGo.AddComponent<PostFx>(); // 빛 번짐·톤 보정·비네팅 (PostFx.cs, P키로 끔)
        FactoryAudio.Init(camGo); // 기계 소리 (Realism.cs)
    }

    // 공장 건물: 에폭시 바닥, 안전선, 벽과 창, 기둥, 지붕 트러스, 천장 크레인
    static void BuildHall()
    {
        var hall = new GameObject("FactoryHall").transform;
        // 바닥은 에폭시 코팅이라 반들거리게(조명 아래 하이라이트), 벽은 패널 이음선이 있는 도장 벽으로
        Skin(Box(hall, "Floor", new Vector3(0, -0.05f, 6f), new Vector3(30, 0.1f, 30), new Color(0.36f, 0.50f, 0.43f)), Tx.Floor, 6f, 6f, 0f, 0.52f);
        Skin(Box(hall, "BackWall", new Vector3(0, 4.5f, 18f), new Vector3(30, 9f, 0.3f), new Color(0.86f, 0.87f, 0.88f)), Tx.Wall, 10f, 3f, 0f, 0.12f);
        Skin(Box(hall, "BackWallBand", new Vector3(0, 0.7f, 17.8f), new Vector3(30, 1.4f, 0.1f), new Color(0.30f, 0.40f, 0.52f)), Tx.Wall, 10f, 1f, 0f, 0.3f);
        Skin(Box(hall, "LeftWall", new Vector3(-14f, 4.5f, 6f), new Vector3(0.3f, 9f, 30f), new Color(0.84f, 0.85f, 0.86f)), Tx.Wall, 3f, 10f, 0f, 0.12f);
        Skin(Box(hall, "RightWall", new Vector3(14f, 4.5f, 6f), new Vector3(0.3f, 9f, 30f), new Color(0.84f, 0.85f, 0.86f)), Tx.Wall, 3f, 10f, 0f, 0.12f);
        for (int i = 0; i < 5; i++)
            Box(hall, "Window" + i, new Vector3(-10f + i * 5f, 6.2f, 17.8f), new Vector3(3.2f, 1.8f, 0.1f), new Color(0.07f, 0.10f, 0.20f)); // 야간: 창밖이 어둡다

        // 통로 안전선과 설비 구획선
        float[] laneZ = { -2.7f, 3.4f, 9.3f, 14.6f };
        foreach (float z in laneZ)
            Box(hall, "Lane", new Vector3(0, 0.005f, z), new Vector3(27f, 0.01f, 0.12f), SafetyYellow);
        Box(hall, "LaneL", new Vector3(-9.6f, 0.005f, 6f), new Vector3(0.12f, 0.01f, 23f), SafetyYellow);
        Box(hall, "LaneR", new Vector3(9.6f, 0.005f, 6f), new Vector3(0.12f, 0.01f, 23f), SafetyYellow);
        Box(hall, "Walkway", new Vector3(0, 0.004f, 4.3f), new Vector3(27f, 0.01f, 1.4f), new Color(0.30f, 0.43f, 0.37f));

        // 기둥, 지붕 트러스
        float[] colZ = { 3.4f, 9.3f, 15.2f };
        foreach (float z in colZ)
        {
            Box(hall, "ColumnL", new Vector3(-11.5f, 4.5f, z), new Vector3(0.45f, 9f, 0.45f), new Color(0.40f, 0.45f, 0.52f));
            Box(hall, "ColumnR", new Vector3(11.5f, 4.5f, z), new Vector3(0.45f, 9f, 0.45f), new Color(0.40f, 0.45f, 0.52f));
            Box(hall, "Truss", new Vector3(0, 8.7f, z), new Vector3(23.5f, 0.35f, 0.35f), new Color(0.40f, 0.45f, 0.52f));
        }

        // 천장 크레인
        Box(hall, "CraneRailL", new Vector3(-11.0f, 7.4f, 9.3f), new Vector3(0.3f, 0.3f, 17f), new Color(0.40f, 0.45f, 0.52f));
        Box(hall, "CraneRailR", new Vector3(11.0f, 7.4f, 9.3f), new Vector3(0.3f, 0.3f, 17f), new Color(0.40f, 0.45f, 0.52f));
        Box(hall, "CraneBridge", new Vector3(0, 7.1f, 9.3f), new Vector3(22.3f, 0.55f, 0.7f), SafetyYellow);
        Box(hall, "Hoist", new Vector3(2.4f, 6.55f, 9.3f), new Vector3(0.9f, 0.6f, 0.9f), Dark);
        Box(hall, "HoistCable", new Vector3(2.4f, 5.5f, 9.3f), new Vector3(0.05f, 1.6f, 0.05f), Dark);
        Box(hall, "Hook", new Vector3(2.4f, 4.6f, 9.3f), new Vector3(0.25f, 0.25f, 0.12f), SafetyYellow);
    }

    // 공장 안쪽의 도어 제작 라인: 천장 컨베이어에 매달린 도어가 이동하고, 작업대의 작업자가 트림을 붙인다
    static void BuildDoorLine(Machine m)
    {
        var line = new GameObject("DoorLine").transform;
        const float z = 13.6f;
        Box(line, "OverheadRail", new Vector3(0, 4.0f, z), new Vector3(22f, 0.14f, 0.14f), SafetyYellow);
        for (int i = 0; i < 5; i++)
            Box(line, "RailHanger" + i, new Vector3(-9f + i * 4.5f, 6.3f, z), new Vector3(0.08f, 4.6f, 0.08f), new Color(0.40f, 0.45f, 0.52f));

        Color[] paints =
        {
            new Color(0.78f, 0.80f, 0.83f), new Color(0.93f, 0.93f, 0.92f), new Color(0.13f, 0.27f, 0.55f),
            new Color(0.60f, 0.10f, 0.10f), new Color(0.25f, 0.27f, 0.30f)
        };
        const int count = 8;
        for (int i = 0; i < count; i++)
        {
            var door = new GameObject("Door" + i).transform;
            door.SetParent(line, false);
            Color c = paints[i % paints.Length];
            Box(door, "Hook", new Vector3(0, 3.82f, 0), new Vector3(0.05f, 0.3f, 0.05f), Dark);
            Skin(Box(door, "Panel", new Vector3(0, 2.55f, 0), new Vector3(1.3f, 0.85f, 0.08f), c), Tx.Paint, 1f, 1f, 0.25f, 0.6f); // 도장한 철판의 광택
            Box(door, "PillarL", new Vector3(-0.61f, 3.28f, 0), new Vector3(0.08f, 0.62f, 0.08f), c);
            Box(door, "PillarR", new Vector3(0.61f, 3.28f, 0), new Vector3(0.08f, 0.62f, 0.08f), c);
            Box(door, "Top", new Vector3(0, 3.63f, 0), new Vector3(1.3f, 0.08f, 0.08f), c);
            // 우리 라인에서 사출한 도어 트림 (카메라 쪽 면에 붙는다)
            var trim = Box(door, "Trim", new Vector3(0, 2.55f, -0.06f), new Vector3(1.12f, 0.68f, 0.04f), new Color(0.10f, 0.10f, 0.11f));
            float span = Machine.DoorLineEnd - Machine.DoorLineStart;
            door.position = new Vector3(Machine.DoorLineStart + span * i / count, 0, z);
            m.hangingDoors.Add(door);
            m.doorTrims.Add(trim);
        }

        // 트림 장착 작업대와 작업자
        Box(line, "Platform", new Vector3(0.6f, 0.7f, z + 1.0f), new Vector3(5.0f, 1.4f, 1.3f), new Color(0.45f, 0.47f, 0.50f));
        Box(line, "PlatformRail", new Vector3(0.6f, 2.45f, z + 1.6f), new Vector3(5.0f, 0.06f, 0.06f), SafetyYellow);
        BuildWorker(m, line, new Vector3(-0.6f, 1.4f, z + 0.75f), 180f, WorkerMode.Assemble, new Color(0.96f, 0.45f, 0.06f), SafetyYellow);
        BuildWorker(m, line, new Vector3(1.6f, 1.4f, z + 0.75f), 180f, WorkerMode.Assemble, new Color(0.72f, 0.86f, 0.10f), Color.white);
        // 트림 부품 대차
        Box(line, "TrimCart", new Vector3(3.9f, 0.9f, z + 0.9f), new Vector3(1.2f, 1.8f, 0.9f), new Color(0.20f, 0.33f, 0.47f));
        Box(line, "TrimStack", new Vector3(3.9f, 2.0f, z + 0.9f), new Vector3(1.0f, 0.4f, 0.7f), new Color(0.10f, 0.10f, 0.11f));
    }

    // 벽의 안전제일 표지판과 라인 표지판. 글자는 WorldLabel로 화면에 겹쳐 그린다
    static void BuildSigns(Machine m)
    {
        var signs = new GameObject("Signs").transform;
        Color green = new Color(0.02f, 0.50f, 0.22f);
        // 안전제일: 뒤쪽 벽 가운데 오른쪽, 창 아래
        Box(signs, "SafetyBorder", new Vector3(1.6f, 4.75f, 17.72f), new Vector3(6.4f, 1.3f, 0.04f), green);
        Glow(Box(signs, "SafetyBoard", new Vector3(1.6f, 4.75f, 17.68f), new Vector3(6.2f, 1.1f, 0.04f), Color.white), Color.white, 0.75f); // 어두운 벽에서도 글씨가 읽히게 스스로 빛난다
        Box(signs, "CrossH", new Vector3(-0.8f, 4.75f, 17.62f), new Vector3(0.75f, 0.24f, 0.04f), green);
        Box(signs, "CrossV", new Vector3(-0.8f, 4.75f, 17.62f), new Vector3(0.24f, 0.75f, 0.04f), green);
        m.labels.Add(new WorldLabel { position = new Vector3(2.1f, 4.75f, 17.6f), text = "안 전 제 일", height = 0.78f, color = green });

        // 도어 조립 라인: 컨베이어 왼쪽 위
        Glow(Box(signs, "DoorLineBoard", new Vector3(-3.6f, 4.5f, 13.3f), new Vector3(3.0f, 0.55f, 0.05f), new Color(0.10f, 0.20f, 0.42f)), new Color(0.10f, 0.20f, 0.42f), 0.6f);
        m.labels.Add(new WorldLabel { position = new Vector3(-3.6f, 4.5f, 13.25f), text = "도어 조립 라인", height = 0.36f, color = Color.white });

        // 주인공 이름표: 조작반 앞 작업자 머리 위
        Glow(Box(signs, "NamePlate", new Vector3(1.75f, 2.3f, -1.85f), new Vector3(1.7f, 0.3f, 0.02f), Color.white), Color.white, 0.45f);
        m.labels.Add(new WorldLabel { position = new Vector3(1.75f, 2.3f, -1.87f), text = TacitData.PersonaName + " · 입사 4주차", height = 0.2f, color = new Color(0.08f, 0.10f, 0.14f), isSelf = true });

        // 사출 1라인: 훈련 대상 설비 위에 매단 흰 표지판
        Glow(Box(signs, "MainLineBoard", new Vector3(0.9f, 3.35f, 0.3f), new Vector3(2.7f, 0.46f, 0.05f), Color.white), Color.white, 0.45f);
        Box(signs, "MainLinePostL", new Vector3(-0.3f, 3.05f, 0.3f), new Vector3(0.05f, 0.9f, 0.05f), Dark);
        Box(signs, "MainLinePostR", new Vector3(2.1f, 3.05f, 0.3f), new Vector3(0.05f, 0.9f, 0.05f), Dark);
        m.labels.Add(new WorldLabel { position = new Vector3(0.9f, 3.35f, 0.25f), text = "사출 1라인 · 도어 트림", height = 0.30f, color = new Color(0.08f, 0.16f, 0.36f) });
    }

    // 사출기 한 대를 parent 아래에 만들고 가동 플래튼을 돌려준다
    static Transform BuildMachine(Transform parent, Color body)
    {
        // 기계 몸체는 도장면(약한 광택), 플래튼·타이바는 솔질 자국이 있는 금속
        Skin(Box(parent, "Bed", new Vector3(0, 0.4f, 0), new Vector3(6.2f, 0.8f, 1.5f), body), Tx.Paint, 2f, 1f, 0.1f, 0.38f);
        Skin(Box(parent, "FixedPlaten", new Vector3(-0.5f, 1.5f, 0), new Vector3(0.2f, 1.4f, 1.4f), Steel), Tx.Steel, 1f, 1f, 0.35f, 0.5f);
        Skin(Box(parent, "RearPlaten", new Vector3(-2.7f, 1.5f, 0), new Vector3(0.2f, 1.4f, 1.4f), Steel), Tx.Steel, 1f, 1f, 0.35f, 0.5f);
        for (int i = 0; i < 4; i++)
        {
            float y = i < 2 ? 0.98f : 2.02f;
            float z = i % 2 == 0 ? -0.56f : 0.56f;
            Skin(Rod(parent, "TieBar" + i, new Vector3(-1.6f, y, z), 2.2f, 0.09f, new Color(0.80f, 0.82f, 0.85f)), Tx.Steel, 1f, 1f, 0.55f, 0.65f);
        }
        var platen = Skin(Box(parent, "MovingPlaten", new Vector3(Machine.OpenX, 1.5f, 0), new Vector3(0.2f, 1.3f, 1.3f), Steel), Tx.Steel, 1f, 1f, 0.35f, 0.5f);
        BuildMachineExtras(parent, body); // 안전 가드, 유압 호스, 받침 트레이 (Realism.cs)
        Box(parent, "MoldFixed", new Vector3(-0.78f, 1.5f, 0), new Vector3(0.35f, 0.8f, 0.8f), Mold);
        // 가동측 금형은 플래튼의 자식 (플래튼 스케일의 역수를 곱해 실제 크기를 맞춘다)
        var mm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mm.name = "MoldMoving";
        mm.transform.SetParent(platen.transform, false);
        mm.transform.localPosition = new Vector3(0.275f / 0.2f, 0, 0);
        mm.transform.localScale = new Vector3(0.35f / 0.2f, 0.8f / 1.3f, 0.8f / 1.3f);
        Paint(mm, Mold);

        // 사출부: 배럴, 히터 밴드, 호퍼, 구동부
        Skin(Rod(parent, "Barrel", new Vector3(0.7f, 1.5f, 0), 2.2f, 0.34f, Steel), Tx.Steel, 1f, 1f, 0.5f, 0.6f);
        for (int i = 0; i < 3; i++)
            Glow(Rod(parent, "Heater" + i, new Vector3(0.1f + i * 0.6f, 1.5f, 0), 0.22f, 0.44f, new Color(0.85f, 0.42f, 0.16f)), new Color(0.85f, 0.30f, 0.06f), 0.35f); // 가열 중인 히터 밴드는 은은히 달아오른다
        Box(parent, "HopperNeck", new Vector3(1.55f, 1.85f, 0), new Vector3(0.25f, 0.4f, 0.25f), Steel);
        var hopper = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hopper.name = "Hopper";
        hopper.transform.SetParent(parent, false);
        hopper.transform.localPosition = new Vector3(1.55f, 2.4f, 0);
        hopper.transform.localScale = new Vector3(0.75f, 0.38f, 0.75f);
        Paint(hopper, new Color(0.90f, 0.91f, 0.93f));
        Skin(Box(parent, "DriveUnit", new Vector3(2.35f, 1.35f, 0), new Vector3(1.4f, 1.1f, 1.2f), body), Tx.Paint, 1f, 1f, 0.1f, 0.38f);
        // 경광등: 켜져 있는 동안 은은히 깜빡인다 (TrainingGame.Update)
        Box(parent, "BeaconPole", new Vector3(2.9f, 2.2f, 0.4f), new Vector3(0.05f, 0.6f, 0.05f), Dark);
        lastBeacon = Glow(Box(parent, "Beacon", new Vector3(2.9f, 2.6f, 0.4f), new Vector3(0.12f, 0.2f, 0.12f), LampOn), LampOn, 1.2f);
        return platen.transform;
    }

    static void BuildConsole(Transform parent, Machine m)
    {
        Box(parent, "ConsolePost", new Vector3(1.0f, 0.75f, -1.15f), new Vector3(0.12f, 1.5f, 0.12f), Dark);
        Box(parent, "Console", new Vector3(1.0f, 1.6f, -1.2f), new Vector3(1.2f, 0.7f, 0.12f), Dark);
        // 조작반 화면은 스스로 빛나서 어두운 공장에서도 또렷하다
        m.screen = Glow(Box(parent, "Screen", new Vector3(1.0f, 1.72f, -1.27f), new Vector3(1.0f, 0.34f, 0.02f), new Color(0.10f, 0.32f, 0.50f)), new Color(0.12f, 0.45f, 0.85f), 0.9f);
        for (int i = 0; i < 4; i++) // 화면의 그래프 줄
            Glow(Box(parent, "ScreenLine" + i, new Vector3(0.68f + i * 0.2f, 1.72f, -1.285f), new Vector3(0.12f, 0.05f + 0.045f * ((i * 5) % 4), 0.01f), new Color(0.75f, 0.92f, 1f)), new Color(0.75f, 0.92f, 1f), 1.1f);
        m.lamps = new Renderer[5];
        for (int i = 0; i < 5; i++)
        {
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "Lamp" + i;
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = new Vector3(0.6f + i * 0.2f, 1.4f, -1.28f);
            lamp.transform.localScale = Vector3.one * 0.12f;
            m.lamps[i] = Glow(Paint(lamp, LampOff), Color.black, 1f); // 색은 Update에서 바꾼다
        }
        // 비상정지 버튼과 조작 스위치
        var estopBase = Box(parent, "EStopBase", new Vector3(1.48f, 1.4f, -1.28f), new Vector3(0.16f, 0.16f, 0.03f), SafetyYellow);
        var estop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        estop.name = "EStop";
        estop.transform.SetParent(parent, false);
        estop.transform.localPosition = new Vector3(1.48f, 1.4f, -1.31f);
        estop.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        estop.transform.localScale = new Vector3(0.12f, 0.025f, 0.12f);
        Paint(estop, new Color(0.80f, 0.08f, 0.06f));
        for (int i = 0; i < 3; i++)
            Box(parent, "Switch" + i, new Vector3(0.62f + i * 0.14f, 1.25f, -1.275f), new Vector3(0.08f, 0.08f, 0.03f), i == 0 ? new Color(0.1f, 0.5f, 0.15f) : new Color(0.18f, 0.18f, 0.2f));
    }

    static void Pallet(Transform parent, Vector3 pos)
    {
        Skin(Box(parent, "Pallet", pos + new Vector3(0, 0.08f, 0), new Vector3(1.3f, 0.16f, 1.1f), new Color(0.62f, 0.48f, 0.30f)), Tx.Wood, 2f, 1f, 0f, 0.08f);
        for (int i = 0; i < 4; i++)
            Skin(Box(parent, "ResinBag" + i, pos + new Vector3(0, 0.27f + i * 0.2f, 0), new Vector3(1.15f - i * 0.03f, 0.18f, 0.95f),
                i % 2 == 0 ? new Color(0.93f, 0.92f, 0.88f) : new Color(0.88f, 0.87f, 0.82f)), Tx.Paper, 1f, 1f, 0f, 0.25f);
    }

    // 현장 근로자: 작업복, 반사띠 안전조끼, 챙 달린 안전모, 목장갑, 안전화. 키 약 1.72m, 앞은 +z
    // 어깨·골반·머리에 회전축을 두어 TrainingGame에서 동작을 준다
    static WorkerRig BuildWorker(Machine m, Transform parent, Vector3 pos, float yaw, WorkerMode mode, Color vest, Color helmetColor)
    {
        // 무료 사람 캐릭터 자산이 있으면 그것으로, 없으면 아래의 기본 도형 작업자로
        var real = BuildRealWorker(m, parent, pos, yaw, mode, vest, helmetColor);
        if (real != null) return real;

        var rig = new WorkerRig { mode = mode, phase = m.workers.Count * 1.7f };
        var w = new GameObject("Worker").transform;
        w.SetParent(parent, false);
        rig.root = w;

        // 사람마다 피부색·머리색·키·체격을 달리해 같은 사람이 복제된 느낌을 없앤다
        int idx = m.workers.Count;
        Color[] jackets = { new Color(0.33f, 0.40f, 0.50f), new Color(0.28f, 0.36f, 0.32f), new Color(0.40f, 0.42f, 0.46f), new Color(0.26f, 0.32f, 0.44f) };
        Color jacket = jackets[idx % jackets.Length]; // 사람마다 작업복 색이 조금씩 다르다
        Color pants = new Color(0.14f, 0.19f, 0.30f);
        Color skin = FaceStyles[idx % FaceStyles.Length].skin;   // 얼굴 질감과 같은 값 (목·귀·손이 이어지게)
        Color hair = FaceStyles[idx % FaceStyles.Length].hair;
        Color boot = new Color(0.10f, 0.09f, 0.09f);
        Color glove = new Color(0.94f, 0.94f, 0.90f);
        Color stripe = new Color(0.86f, 0.88f, 0.90f);
        float tall = WorkerHeights[idx % WorkerHeights.Length];
        float wide = WorkerBuild[idx % WorkerBuild.Length];

        // 몸통: 상체를 한 축(chest)에 묶어 숨쉬기와 몸 기울임을 줄 수 있게 한다
        Ball(w, "Pelvis", new Vector3(0, 0.95f, 0), new Vector3(0.36f, 0.22f, 0.25f), pants);
        Box(w, "Belt", new Vector3(0, 1.06f, 0), new Vector3(0.37f, 0.045f, 0.26f), new Color(0.08f, 0.08f, 0.09f));
        rig.chest = Limb(w, "Chest", new Vector3(0, 1.26f, 0));
        Capsule(rig.chest, "Torso", Vector3.zero, new Vector3(0.40f, 0.31f, 0.25f), jacket, Vector3.zero);
        Capsule(rig.chest, "Vest", new Vector3(0, 0.01f, 0), new Vector3(0.43f, 0.24f, 0.275f), vest, Vector3.zero);
        Box(rig.chest, "StripeTop", new Vector3(0, 0.09f, 0), new Vector3(0.435f, 0.035f, 0.28f), stripe);
        Box(rig.chest, "StripeBottom", new Vector3(0, -0.07f, 0), new Vector3(0.435f, 0.035f, 0.28f), stripe);
        Box(rig.chest, "IdCard", new Vector3(0.08f, 0.0f, 0.145f), new Vector3(0.07f, 0.10f, 0.012f), Color.white);
        Box(rig.chest, "IdBand", new Vector3(0.08f, 0.04f, 0.15f), new Vector3(0.07f, 0.025f, 0.012f), new Color(0.16f, 0.47f, 0.84f));
        rig.chest.localScale = new Vector3(wide, 1f, wide);

        // 다리 (골반 회전축)
        rig.hipL = Limb(w, "HipL", new Vector3(-0.10f, 0.93f, 0));
        rig.hipR = Limb(w, "HipR", new Vector3(0.10f, 0.93f, 0));
        foreach (Transform hip in new[] { rig.hipL, rig.hipR })
        {
            Capsule(hip, "Thigh", new Vector3(0, -0.23f, 0), new Vector3(0.16f, 0.24f, 0.16f), pants, Vector3.zero);
            // 무릎 관절: 종아리 이하가 여기에 매달려 걸을 때 접힌다
            var knee = Limb(hip, "Knee", new Vector3(0, -0.43f, 0));
            Ball(knee, "KneeJoint", Vector3.zero, Vector3.one * 0.14f, pants);
            Capsule(knee, "Shin", new Vector3(0, -0.19f, 0), new Vector3(0.13f, 0.22f, 0.13f), pants, Vector3.zero);
            Box(knee, "Boot", new Vector3(0, -0.45f, 0.04f), new Vector3(0.12f, 0.10f, 0.27f), boot);
            Box(knee, "Sole", new Vector3(0, -0.505f, 0.04f), new Vector3(0.127f, 0.025f, 0.28f), new Color(0.55f, 0.55f, 0.56f)); // 신발 밑창
            Box(knee, "KneePad", new Vector3(0, 0.01f, 0.075f), new Vector3(0.11f, 0.10f, 0.03f), new Color(0.10f, 0.10f, 0.11f)); // 무릎 보호대
            if (hip == rig.hipL) rig.kneeL = knee; else rig.kneeR = knee;
        }

        // 팔 (어깨 회전축)
        rig.shoulderL = Limb(w, "ShoulderL", new Vector3(-0.27f, 1.47f, 0));
        rig.shoulderR = Limb(w, "ShoulderR", new Vector3(0.27f, 1.47f, 0));
        foreach (Transform sh in new[] { rig.shoulderL, rig.shoulderR })
        {
            Ball(sh, "Shoulder", Vector3.zero, Vector3.one * 0.15f, jacket);
            Capsule(sh, "UpperArm", new Vector3(0, -0.15f, 0), new Vector3(0.105f, 0.16f, 0.105f), jacket, Vector3.zero);
            // 팔꿈치 관절: 아래팔과 장갑이 여기에 매달려 굽혀진다
            var elbow = Limb(sh, "Elbow", new Vector3(0, -0.30f, 0));
            Ball(elbow, "ElbowJoint", Vector3.zero, Vector3.one * 0.10f, jacket);
            Capsule(elbow, "Forearm", new Vector3(0, -0.12f, 0), new Vector3(0.09f, 0.14f, 0.09f), jacket, Vector3.zero);
            Ball(elbow, "Glove", new Vector3(0, -0.30f, 0), Vector3.one * 0.10f, glove);
            Box(elbow, "Cuff", new Vector3(0, -0.245f, 0), new Vector3(0.105f, 0.045f, 0.105f), new Color(0.12f, 0.14f, 0.18f)); // 소맷단
            if (sh == rig.shoulderL) rig.elbowL = elbow; else rig.elbowR = elbow;
        }

        // 머리 (목 회전축): 얼굴, 머리카락, 안전모와 챙
        Capsule(w, "Neck", new Vector3(0, 1.60f, 0), new Vector3(0.10f, 0.05f, 0.10f), skin, Vector3.zero);
        rig.head = Limb(w, "Head", new Vector3(0, 1.72f, 0));
        // 얼굴: 그려 넣은 질감을 입힌 머리 모양 (MachineBuilder.People.cs). 눈은 질감에 그려져 있고 깜빡일 때만 눈꺼풀이 내려온다
        MakeFace(rig.head, idx);
        Ball(rig.head, "Hair", new Vector3(0, 0.012f, -0.028f), new Vector3(0.206f, 0.222f, 0.196f), hair);
        Ball(rig.head, "EarL", new Vector3(-0.098f, -0.005f, 0.0f), new Vector3(0.026f, 0.055f, 0.040f), skin);
        Ball(rig.head, "EarR", new Vector3(0.098f, -0.005f, 0.0f), new Vector3(0.026f, 0.055f, 0.040f), skin);
        var lidL = Ball(rig.head, "LidL", new Vector3(-0.031f, 0.0165f, 0.1005f), new Vector3(0.034f, 0.0006f, 0.012f), skin);
        var lidR = Ball(rig.head, "LidR", new Vector3(0.031f, 0.0165f, 0.1005f), new Vector3(0.034f, 0.0006f, 0.012f), skin);
        rig.eyes = new[] { lidL, lidR };
        Ball(rig.head, "Nose", new Vector3(0, -0.012f, 0.1075f), new Vector3(0.028f, 0.040f, 0.030f), skin);
        // 안전모 턱끈
        Box(rig.head, "StrapL", new Vector3(-0.093f, -0.04f, 0.03f), new Vector3(0.010f, 0.12f, 0.012f), new Color(0.08f, 0.08f, 0.09f));
        Box(rig.head, "StrapR", new Vector3(0.093f, -0.04f, 0.03f), new Vector3(0.010f, 0.12f, 0.012f), new Color(0.08f, 0.08f, 0.09f));
        Ball(rig.head, "Helmet", new Vector3(0, 0.112f, 0), new Vector3(0.25f, 0.165f, 0.262f), helmetColor);
        var brim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        brim.name = "HelmetBrim";
        brim.transform.SetParent(rig.head, false);
        brim.transform.localPosition = new Vector3(0, 0.090f, 0.030f);
        brim.transform.localScale = new Vector3(0.27f, 0.008f, 0.27f); // 챙이 눈에 그늘을 크게 드리우지 않게 짧고 높게
        Paint(brim, helmetColor);

        // 옷깃, 명찰·회사 패치, 공구 주머니, 무전기
        Box(w, "Collar", new Vector3(0, 1.545f, 0.005f), new Vector3(0.16f, 0.04f, 0.13f), new Color(0.80f, 0.82f, 0.85f));
        Box(rig.chest, "NamePatch", new Vector3(-0.09f, 0.035f, 0.146f), new Vector3(0.085f, 0.035f, 0.008f), Color.white);
        Box(rig.chest, "NameBar", new Vector3(-0.09f, 0.035f, 0.151f), new Vector3(0.07f, 0.008f, 0.004f), new Color(0.15f, 0.20f, 0.30f));
        Box(rig.chest, "LogoPatch", new Vector3(0.10f, 0.115f, 0.146f), new Vector3(0.06f, 0.05f, 0.008f), new Color(0.92f, 0.41f, 0.20f));
        Box(rig.chest, "Radio", new Vector3(-0.16f, 0.10f, 0.15f), new Vector3(0.05f, 0.10f, 0.03f), new Color(0.08f, 0.08f, 0.09f));
        Box(rig.chest, "RadioAntenna", new Vector3(-0.172f, 0.17f, 0.15f), new Vector3(0.008f, 0.07f, 0.008f), new Color(0.08f, 0.08f, 0.09f));
        Box(w, "Pouch", new Vector3(0.21f, 1.0f, 0.10f), new Vector3(0.10f, 0.14f, 0.07f), new Color(0.38f, 0.28f, 0.18f));
        // 천 질감: 작업복과 바지에 직조 무늬를 입힌다
        foreach (Renderer rr in w.GetComponentsInChildren<Renderer>())
        {
            string nm = rr.name;
            if (nm == "Torso" || nm == "Thigh" || nm == "Shin" || nm == "UpperArm" || nm == "Forearm" || nm == "Pelvis")
                Skin(rr, Tx.Fabric, 3f, 3f, 0f, 0.05f);
        }
        w.localPosition = pos;
        w.localRotation = Quaternion.Euler(0, yaw, 0);
        w.localScale = new Vector3(1f, tall, 1f);
        rig.basePos = pos;
        rig.baseYaw = yaw;
        m.workers.Add(rig);
        return rig;
    }

    static Transform Limb(Transform parent, string name, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        return t;
    }

    static Transform Ball(Transform parent, string name, Vector3 pos, Vector3 scale, Color c)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        Paint(go, c);
        return go.transform;
    }

    public static GameObject MakePart(Machine m, bool defect)
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        p.name = defect ? "PartNG" : "PartOK";
        p.transform.SetParent(m.partsRoot, false);
        p.transform.localScale = new Vector3(0.11f, 0.05f, 0.15f);
        p.transform.position = m.partOrigin;
        // 자동차 내장 부품은 검은색 수지. 불량은 눈에 띄게 붉은색으로 표시
        Paint(p, defect ? new Color(0.92f, 0.28f, 0.16f) : new Color(0.17f, 0.18f, 0.20f));
        return p;
    }

    static Renderer Box(Transform parent, string name, Vector3 pos, Vector3 size, Color c)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        return Paint(go, c);
    }

    // X축 방향 원기둥
    static Renderer Rod(Transform parent, string name, Vector3 pos, float length, float diameter, Color c)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        go.transform.localScale = new Vector3(diameter, length / 2f, diameter);
        return Paint(go, c);
    }

    static void Capsule(Transform parent, string name, Vector3 pos, Vector3 scale, Color c, Vector3 euler)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        Paint(go, c);
    }

    // 빌드에 셰이더가 포함되도록 Resources/Base.mat을 복제해서 쓴다 (없으면 기본 재질)
    public static Renderer Paint(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (baseMat == null) baseMat = Resources.Load<Material>("Base");
        if (baseMat != null) r.sharedMaterial = new Material(baseMat);
        r.material.color = c;
        if (r.material.HasProperty("_Glossiness")) r.material.SetFloat("_Glossiness", 0.2f);
        return r;
    }
}
