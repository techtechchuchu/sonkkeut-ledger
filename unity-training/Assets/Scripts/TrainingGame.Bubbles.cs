using UnityEngine;

// 작업자가 말할 때 화면 아래에 뜨는 자막 (소리는 내장 한국어 음성으로 따로 나온다)
public partial class TrainingGame
{
    GUIStyle sSub, sSubName, sSubWrap;

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
        // 왼쪽 조건 패널과 오른쪽 태블릿 사이의 빈 곳 안에서만 그리고, 길면 줄을 바꾼다
        float maxW = Mathf.Max(380f, W - 1192f); // 생산 패널(왼쪽 488 ~ 태블릿 앞)과 같은 폭
        float bw = Mathf.Min(nameSize.x + textSize.x + 64f, maxW);
        float textW = bw - nameSize.x - 52f;
        if (sSubWrap == null) sSubWrap = new GUIStyle(sSub) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
        float bh = Mathf.Max(52f, sSubWrap.CalcHeight(new GUIContent(line), textW) + 16f);
        float cx = 488 + Mathf.Max(380f, W - 1192f) / 2f;
        var r = new Rect(cx - bw / 2f, 792f - bh, bw, bh); // 아랫변은 생산 패널 위에 고정

        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.70f * a);
        GUI.DrawTexture(r, white);
        GUI.color = new Color(1f, 0.55f, 0.2f, a);
        GUI.DrawTexture(new Rect(r.x, r.y, 5f, r.height), white); // 왼쪽 주황 띠
        GUI.color = new Color(1f, 1f, 1f, a);
        GUI.Label(new Rect(r.x + 20f, r.y, nameSize.x + 4f, r.height), who, sSubName);
        GUI.Label(new Rect(r.x + 32f + nameSize.x, r.y, textW, r.height), line, sSubWrap);
        GUI.color = old;
    }
}
