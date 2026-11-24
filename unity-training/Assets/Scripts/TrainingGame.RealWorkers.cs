using System.Collections.Generic;
using UnityEngine;

// 실제 사람 캐릭터의 동작: 동작 클립(걷기, 서 있기, 말하기, 조작, 집기, 수리)을 상황에 맞게 바꿔 재생한다.
// 대사 소리는 Resources/Voice 의 미리 만든 음성 파일(낮은 남자 목소리로 조정)을 재생한다.
public partial class TrainingGame
{
    // 역할별 대사 묶음의 이름표 (lines.txt 의 앞글자). 같은 조의 두 사람은 번갈아 말하면 대화가 이어지게 썼다
    static readonly float[] VoicePitch = { 0.86f, 0.92f, 0.82f, 0.88f, 0.84f };
    static Dictionary<string, string> voiceText;
    static readonly Dictionary<string, int> voiceCount = new Dictionary<string, int>();

    static void LoadVoiceLines()
    {
        if (voiceText != null) return;
        voiceText = new Dictionary<string, string>();
        var ta = Resources.Load<TextAsset>("Voice/lines");
        if (ta == null) return;
        foreach (string line in ta.text.Split('\n'))
        {
            string l = line.Trim();
            int t = l.IndexOf('\t');
            if (t <= 0) continue;
            string id = l.Substring(0, t);
            voiceText[id] = l.Substring(t + 1);
            string prefix = id.Substring(0, id.Length - 1);
            int n; voiceCount.TryGetValue(prefix, out n);
            voiceCount[prefix] = Mathf.Max(n, int.Parse(id.Substring(id.Length - 1)) + 1);
        }
    }

    static string PrefixFor(WorkerRig w, int idx)
    {
        switch (w.mode)
        {
            case WorkerMode.Operate: return "op";
            case WorkerMode.Walk: return "wk";
            case WorkerMode.Fix: return "fx";
            default: return idx % 2 == 0 ? "aa" : "ab";
        }
    }

    // 음성 파일은 읽는 대로("삼호기") 적혀 있어 자막에는 숫자로 바꿔 보여 준다
    static string Caption(string t) { return t.Replace("삼호기", "3호기").Replace("삼번", "3번"); }

    // 이야기 동작이 시작될 때 대사를 하나 꺼내 자막을 띄우고, 거리에 맞는 크기로 목소리를 낸다
    void SayLine(WorkerRig w, int idx)
    {
        if (CueBusy && !captureSay) return; // 주인공 대사 중에는 다른 사람이 겹쳐 말하지 않는다
        LoadVoiceLines();
        string prefix = PrefixFor(w, idx);
        int n; if (!voiceCount.TryGetValue(prefix, out n) || n == 0) return;
        string id = prefix + (w.lineNo % n);
        w.lineNo++;
        string text; if (!voiceText.TryGetValue(id, out text)) return;
        w.say = Caption(text);
        w.sayStart = Time.time;
        w.sayUntil = Time.time + 3.8f;

        var clip = Resources.Load<AudioClip>("Voice/" + id);
        if (clip == null) return;
        float pitch = VoicePitch[idx % VoicePitch.Length];
        w.sayUntil = Time.time + clip.length / pitch + 0.8f; // 목소리가 끝난 뒤 잠깐 더 보인다
        if (FactoryAudio.Muted || VoiceOff || machine.cam == null) return;
        float dist = Vector3.Distance(machine.cam.transform.position, w.root.position);
        float vol = Mathf.Clamp01(1.3f - dist / 15f);
        if (vol < 0.05f) return;
        var src = w.root.GetComponent<AudioSource>();
        if (src == null)
        {
            src = w.root.gameObject.AddComponent<AudioSource>();
            src.spatialBlend = 0.7f; src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 2f; src.maxDistance = 22f; src.playOnAwake = false;
        }
        if (src.isPlaying) return;
        src.pitch = pitch;
        src.volume = vol;
        src.clip = clip;
        src.Play();
    }

    static void SetState(WorkerRig w, string state, float fade = 0.35f)
    {
        if (w.curState == state) return;
        w.curState = state;
        w.animator.CrossFadeInFixedTime(state, fade);
    }

    // true 이면 목소리를 재생하지 않는다(자막은 그대로). 음성 파일은 Resources/Voice 에 남겨 두었으니 false 로 바꾸면 다시 나온다
    const bool VoiceOff = true;
    bool captureSay;
    bool captureHammer = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-hammer") >= 0;

    void AnimateRealWorker(WorkerRig w)
    {
        int idx = machine.workers.IndexOf(w);
        if (!w.yawInit)
        {
            // 사람마다 동작 속도를 조금씩 달리하고(±8%), 처음 서 있던 방향을 기억해 둔다
            w.yawInit = true;
            w.baseYaw = w.root.eulerAngles.y;
            w.animSpeed = 0.92f + 0.16f * Mathf.Abs(Mathf.Sin(w.phase * 12.9898f + idx * 4.1f));
            w.animator.speed = w.animSpeed;
        }
        if (captureSay && Time.time > 1.0f && w.lineNo == 0 && w.mode == WorkerMode.Assemble && idx % 2 == 1) { SayLine(w, idx); w.sayUntil = Time.time + 12f; }
        // 가끔(작업자마다 다른 때) 몇 초 동안 서로 이야기하는 동작
        bool talking = Gesture(w, 21f, 4.5f, 3f) > 0.01f;
        if (talking && !w.wasTalking) SayLine(w, idx); // 이야기 동작에 들어가는 순간 한 마디
        w.wasTalking = talking;

        switch (w.mode)
        {
            case WorkerMode.Operate:
            {
                // 생산 중에는 조작반을 만지다가 가끔 손을 멈추고 화면을 살핀다. 쉴 때는 서 있다가 가끔 이야기
                bool glance = Gesture(w, 13f, 2.6f, 5f) > 0.01f;
                SetState(w, running && !glance ? "Interact" : (talking ? "Idle_Talking_Loop" : "Idle_Loop"), 0.5f);
                break;
            }
            case WorkerMode.Assemble:
            {
                // 트림을 끼우는 두 사람: 집기 → 끼우기를 번갈아 하는 작업 리듬. 가끔 멈추고 서로 마주 보며 이야기
                float c = (Time.time + w.phase * 3f + idx * 2.3f) % 11f;
                string st = c < 4.5f ? "PickUp_Table" : (c < 10f ? "Interact" : "Idle_Loop");
                SetState(w, talking ? "Idle_Talking_Loop" : st, 0.5f);
                // 이야기할 땐 짝 쪽으로 몸을 돌리고, 끝나면 원래 방향으로 돌아온다
                WorkerRig mate = null; float best = 1e9f;
                foreach (var o in machine.workers)
                    if (o != w && o.mode == WorkerMode.Assemble)
                    {
                        float dd = (o.root.position - w.root.position).sqrMagnitude;
                        if (dd < best) { best = dd; mate = o; }
                    }
                float yaw = w.baseYaw;
                if (talking && mate != null)
                {
                    Vector3 to = mate.root.position - w.root.position; to.y = 0f;
                    if (to.sqrMagnitude > 0.01f) yaw = Quaternion.LookRotation(to).eulerAngles.y;
                }
                w.root.rotation = Quaternion.Slerp(w.root.rotation, Quaternion.Euler(0, yaw, 0), 1f - Mathf.Exp(-3f * Time.deltaTime));
                break;
            }
            case WorkerMode.Fix:
            {
                // 약 16초 순환: 무릎 꿇고 렌치로 조이다가(8초), 잠깐 일어서서 공구를 바꿔 들고, 전동 드릴로 조인다(7초)
                float t = (Time.time + w.phase * 3f) % 16f;
                bool drill = t >= 9f, kneel = t < 8.2f;
                if (captureHammer) { drill = true; kneel = false; }
                SetState(w, kneel ? "Fixing_Kneeling" : (drill ? "Interact" : "Idle_Loop"), 0.5f);
                if (w.hammer != null) w.hammer.SetActive(drill);
                if (w.wrench != null) w.wrench.SetActive(kneel);
                break;
            }
            case WorkerMode.Walk:
            {
                bool paused = Time.time < w.pauseUntil;
                Vector3 p = w.root.position;
                // 걸음 속도는 천천히 붙고 천천히 줄어든다 (갑자기 멈추거나 튀어나가지 않게)
                w.walkSpeed = Mathf.MoveTowards(w.walkSpeed, paused ? 0f : 1.15f, 1.1f * Time.deltaTime);
                if (w.walkSpeed > 0.01f)
                {
                    p.x += w.walkDir * w.walkSpeed * Time.deltaTime;
                    if (!paused && (p.x > w.walkMaxX || p.x < w.walkMinX))
                    {
                        w.walkDir = p.x > w.walkMaxX ? -1f : 1f;
                        w.pauseUntil = Time.time + 2.2f + 1.8f * Mathf.Abs(Mathf.Sin(w.phase * 7f + Time.time));
                    }
                    w.root.position = p;
                }
                bool walking = w.walkSpeed > 0.25f;
                // 걸을 땐 가는 방향, 멈추면 기계(앞쪽 -z)를 본다. 방향은 부드럽게 바뀐다
                Quaternion face = walking ? Quaternion.Euler(0, w.walkDir > 0 ? 90f : -90f, 0) : Quaternion.Euler(0, 180f, 0);
                w.root.rotation = Quaternion.Slerp(w.root.rotation, face, 1f - Mathf.Exp(-3f * Time.deltaTime));
                SetState(w, walking ? "Walk_Loop" : (talking ? "Idle_Talking_Loop" : "Idle_Loop"), 0.45f);
                // 발이 미끄러지지 않게 걷는 속도에 맞춰 걸음 동작 속도를 조절한다
                w.animator.speed = walking ? w.animSpeed * Mathf.Clamp(w.walkSpeed / 1.15f, 0.6f, 1.05f) : w.animSpeed;
                break;
            }
        }
    }
}
