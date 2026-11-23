using UnityEngine;

// 작업자가 말할 때 화면 아래에 뜨는 자막 (소리는 내장 한국어 음성으로 따로 나온다)
public partial class TrainingGame
{
    GUIStyle sSub, sSubName;

    static string RoleName(WorkerRig w, int idx)
    {
        switch (w.mode)
        {
            case WorkerMode.Operate: return TacitData.PersonaName;
            case WorkerMode.Walk: return "순찰 작업자";
            case WorkerMode.Fix: return "정비 작업자";
            default: return idx % 2 == 0 ? "조립 작업자 A" : "조립 작업자 B";
        }
    }

    // 지금 말하고 있는 사람 중 가장 최근에 시작한 한 명의 말을 자막으로 보여 준다
    void DrawSubtitle(float W)
    {
        if (machine == null) return;
        // 주인공의 상황 대사가 있으면 그것만 보여 준다 (소리와 같은 대사)
        bool cueOn = Time.time < cueUntil && !string.IsNullOrEmpty(cueShown);
        WorkerRig cur = null;
        int curIdx = -1;
        string who, line;
        float sStart, sUntil;
        if (cueOn)
        {
            who = cueName; line = cueShown; sStart = cueStart; sUntil = cueUntil;
        }
        else
        {
            for (int i = 0; i < machine.workers.Count; i++)
            {
                WorkerRig w = machine.workers[i];
                if (w.animator == null || string.IsNullOrEmpty(w.say) || Time.time > w.sayUntil) continue;
                if (cur == null || w.sayStart > cur.sayStart) { cur = w; curIdx = i; }
            }
            if (cur == null) return;
            who = RoleName(cur, curIdx); line = cur.say; sStart = cur.sayStart; sUntil = cur.sayUntil;
        }

        if (sSub == null)
        {
            sSub = new GUIStyle(sLabel) { fontSize = 26, alignment = TextAnchor.MiddleLeft };
            sSub.normal.textColor = Color.white;
            sSubName = new GUIStyle(sSub) { fontSize = 20, fontStyle = FontStyle.Bold };
            sSubName.normal.textColor = new Color(1f, 0.70f, 0.48f);
        }
        float tin = Mathf.Clamp01((Time.time - sStart) / 0.2f);
        float tout = Mathf.Clamp01((sUntil - Time.time) / 0.4f);
        float a = Mathf.Min(tin, tout);

        Vector2 nameSize = sSubName.CalcSize(new GUIContent(who));
        Vector2 textSize = sSub.CalcSize(new GUIContent(line));
        float bw = nameSize.x + textSize.x + 64f, bh = 52f;
        float cx = 488 + (W - 1076) / 2f;
        var r = new Rect(cx - bw / 2f, 1080 - 24 - 250 - 66f, bw, bh);

        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.70f * a);
        GUI.DrawTexture(r, white);
        GUI.color = new Color(1f, 0.55f, 0.2f, a);
        GUI.DrawTexture(new Rect(r.x, r.y, 5f, r.height), white); // 왼쪽 주황 띠
        GUI.color = new Color(1f, 1f, 1f, a);
        GUI.Label(new Rect(r.x + 20f, r.y, nameSize.x + 4f, r.height), who, sSubName);
        GUI.Label(new Rect(r.x + 32f + nameSize.x, r.y, textSize.x + 8f, r.height), line, sSub);
        GUI.color = old;
    }
}
