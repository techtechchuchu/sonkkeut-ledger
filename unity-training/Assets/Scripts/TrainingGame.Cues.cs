using System.Collections.Generic;
using UnityEngine;

// 상황 대사: 주인공 김민준이 지금 벌어지는 일에 맞춰 한 마디씩 한다.
// 자막과 목소리는 같은 대사에서 나오고, 자막은 목소리가 끝날 때까지 보인다.
// 대사 목록은 Resources/Voice/cues.txt (번호, 자막, 읽는 말), 소리는 Voice/c_<번호>.wav
public partial class TrainingGame
{
    static Dictionary<string, string> cueText;
    static Dictionary<string, string> cueWho; // 말하는 사람 (비어 있으면 주인공)
    string cueNow, cueShown, cueName;
    float cueStart, cueUntil;
    readonly List<string> cueQueue = new List<string>();
    readonly Dictionary<string, float> cueLast = new Dictionary<string, float>();
    AudioSource cueSrc;
    const float CuePitch = 0.86f; // 낮은 남자 목소리로 들리게

    static void LoadCues()
    {
        if (cueText != null) return;
        cueText = new Dictionary<string, string>();
        cueWho = new Dictionary<string, string>();
        var ta = Resources.Load<TextAsset>("Voice/cues");
        if (ta == null) return;
        foreach (string line in ta.text.Split('\n'))
        {
            string[] p = line.TrimEnd('\r').Split('\t');
            if (p.Length < 2 || p[0].Length == 0) continue;
            cueText[p[0]] = p[1];
            cueWho[p[0]] = p.Length >= 4 ? p[3].Trim() : "";
        }
    }

    // 사람마다 목소리 높낮이를 달리해 서로 다른 사람처럼 들리게 한다
    static float PitchFor(string who)
    {
        if (who == "조립 작업자 A") return 0.94f;
        if (who == "조립 작업자 B") return 0.84f;
        if (who == "정비 작업자") return 0.80f;
        return CuePitch;
    }

    static string SpeakerName(string who)
    {
        return (string.IsNullOrEmpty(who) || who == "민준") ? TacitData.PersonaName : who;
    }

    // ---------- 말하는 사람 쪽으로 카메라가 다가가는 연출 ----------
    WorkerRig shotWorker;   // 지금 대사를 하는 작업자 (주인공이면 null: 1인칭 그대로)
    float shotK;            // 0 = 내 시점, 1 = 말하는 사람 앞

    WorkerRig FindSpeaker(string who)
    {
        if (machine == null) return null;
        bool a = who == "조립 작업자 A", b = who == "조립 작업자 B", fx = who == "정비 작업자";
        for (int i = 0; i < machine.workers.Count; i++)
        {
            WorkerRig w = machine.workers[i];
            if (w.animator == null) continue;
            if (fx && w.mode == WorkerMode.Fix) return w;
            if ((a || b) && w.mode == WorkerMode.Assemble && ((i % 2 == 0) == a)) return w;
        }
        return null;
    }

    // 말하는 사람의 얼굴이 보이는 자리: 앞쪽·옆쪽 중 벽이나 설비에 가리지 않는 곳을 고른다
    void ShotPose(WorkerRig w, out Vector3 pos, out Quaternion rot)
    {
        Vector3 head = w.root.position + Vector3.up * 1.5f;
        Vector3 f = w.root.forward, r = w.root.right;
        Vector3[] dirs = { (f + r * 0.45f).normalized, (f - r * 0.45f).normalized, f, r, -r };
        pos = head + dirs[0] * 1.5f;
        foreach (float dist in new[] { 2.3f, 1.7f })
            foreach (Vector3 d in dirs)
            {
                Vector3 p = head + d * dist + Vector3.up * 0.05f;
                RaycastHit hit;
                bool blocked = Physics.Linecast(head, p, out hit, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(w.root);
                if (!blocked) { pos = p; goto done; }
            }
        done:
        rot = Quaternion.LookRotation((head - Vector3.up * 0.05f) - pos);
    }

    bool CueBusy { get { return Time.time < cueUntil || cueQueue.Count > 0; } }

    // 지금 대사 중이면 건너뛰는 가벼운 혼잣말 / queue=true 면 차례를 기다렸다가 꼭 말하는 중요한 대사
    void Cue(string id, bool queue = false, float cooldown = 20f)
    {
        LoadCues();
        if (!cueText.ContainsKey(id)) return;
        float last;
        if (cueLast.TryGetValue(id, out last) && Time.time - last < cooldown) return;
        if (CueBusy)
        {
            if (!queue) return;
            cueLast[id] = Time.time;
            cueQueue.Add(id);
            return;
        }
        StartCue(id);
    }

    void StartCue(string id)
    {
        cueLast[id] = Time.time;
        cueNow = id;
        cueShown = cueText[id];
        string who = cueWho.ContainsKey(id) ? cueWho[id] : "";
        cueName = SpeakerName(who);
        shotWorker = FindSpeaker(who);
        float pitch = PitchFor(who);
        cueStart = Time.time;
        var clip = Resources.Load<AudioClip>("Voice/c_" + id);
        float len = clip != null ? clip.length / pitch : 3.2f;
        cueUntil = Time.time + len + 0.7f; // 목소리가 끝난 뒤 잠깐 더 보인다
        if (clip == null || FactoryAudio.Muted) return;
        if (cueSrc == null)
        {
            cueSrc = gameObject.AddComponent<AudioSource>();
            cueSrc.spatialBlend = 0f; cueSrc.playOnAwake = false;
        }
        cueSrc.Stop();
        cueSrc.pitch = pitch;
        cueSrc.volume = 0.95f;
        cueSrc.clip = clip;
        cueSrc.Play();
    }

    void TickCues()
    {
        if (Time.time >= cueUntil && cueQueue.Count > 0)
        {
            string id = cueQueue[0];
            cueQueue.RemoveAt(0);
            StartCue(id);
        }
    }

    // 지금 조건에 맞는 첫 혼잣말
    void CueForConditions()
    {
        if (env.humidity >= 70f) Cue("cond_humid");
        else if (env.temp <= 5f) Cue("cond_cold");
        else if (env.temp >= 28f) Cue("cond_hot");
        else Cue("cond_ok");
    }
}
