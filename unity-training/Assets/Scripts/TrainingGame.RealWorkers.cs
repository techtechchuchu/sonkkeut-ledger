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
        if (FactoryAudio.Muted || machine.cam == null) return;
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

    bool captureSay;
    bool captureHammer = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-hammer") >= 0;

    void AnimateRealWorker(WorkerRig w)
    {
        int idx = machine.workers.IndexOf(w);
        if (captureSay && Time.time > 1.0f && w.lineNo == 0 && w.mode == WorkerMode.Assemble && idx % 2 == 1) { SayLine(w, idx); w.sayUntil = Time.time + 12f; }
        // 가끔(작업자마다 다른 때) 몇 초 동안 서로 이야기하는 동작
        bool talking = Gesture(w, 21f, 4.5f, 3f) > 0.01f;
        if (talking && !w.wasTalking) SayLine(w, idx); // 이야기 동작에 들어가는 순간 한 마디
        w.wasTalking = talking;

        switch (w.mode)
        {
            case WorkerMode.Operate:
                // 생산 중에는 조작반을 만지는 동작, 쉴 때는 서 있다가 가끔 이야기
                SetState(w, running ? "Interact" : (talking ? "Idle_Talking_Loop" : "Idle_Loop"));
                break;
            case WorkerMode.Assemble:
                // 트림을 끼우는 두 사람: 한 사람은 상호작용 동작, 다른 사람은 집는 동작. 가끔 서로 이야기
                if (talking) SetState(w, "Idle_Talking_Loop");
                else SetState(w, idx % 2 == 0 ? "Interact" : "PickUp_Table");
                break;
            case WorkerMode.Fix:
            {
                // 약 16초 순환: 무릎 꿇고 렌치로 조이다가(9초), 일어나서 전동 드릴로 조인다(7초)
                float t = (Time.time + w.phase * 3f) % 16f;
                bool hit = t >= 9f;
                if (captureHammer) hit = true;
                SetState(w, hit ? "Interact" : "Fixing_Kneeling", 0.4f); // 서서는 드릴을 들고 작업
                if (w.hammer != null) w.hammer.SetActive(hit);
                if (w.wrench != null) w.wrench.SetActive(!hit);
                break;
            }
            case WorkerMode.Walk:
            {
                bool paused = Time.time < w.pauseUntil;
                Vector3 p = w.root.position;
                if (!paused)
                {
                    p.x += w.walkDir * 1.15f * Time.deltaTime;
                    if (p.x > w.walkMaxX || p.x < w.walkMinX)
                    {
                        w.walkDir = p.x > w.walkMaxX ? -1f : 1f;
                        w.pauseUntil = Time.time + 2.2f + 1.8f * Mathf.Abs(Mathf.Sin(w.phase * 7f + Time.time));
                    }
                    w.root.position = p;
                }
                // 걸을 땐 가는 방향, 멈추면 기계(앞쪽 -z)를 본다. 방향은 부드럽게 바뀐다
                Quaternion face = paused ? Quaternion.Euler(0, 180f, 0) : Quaternion.Euler(0, w.walkDir > 0 ? 90f : -90f, 0);
                w.root.rotation = Quaternion.Slerp(w.root.rotation, face, 1f - Mathf.Exp(-4f * Time.deltaTime));
                SetState(w, paused ? (talking ? "Idle_Talking_Loop" : "Idle_Loop") : "Walk_Loop", 0.4f);
                break;
            }
        }
    }
}
