using UnityEngine;

// 화면을 가리지 않는 배치와 이야기식 안내.
// - 공장이 넓게 보이도록 조건 창은 한 줄로 접어 두고(누르면 펼침), 에이전트 태블릿은 오른쪽 아래에 작게 들고 있다가 누르면 들어 올린다.
// - 아래쪽 안내 띠가 지금 할 일을 한 단계씩 알려 준다: 태블릿 열기 → 본 대로 적기 → 노하우 따르기 → 생산 → 보상 원장.
public partial class TrainingGame
{
    bool condOpen;      // 조건 창을 펼쳤는가
    bool tabletOpen;    // 태블릿을 들어 올렸는가
    GUIStyle sStory, sStoryNo, sMiniTitle, sMiniText, sClear;

    const float TabletW = 540f, MiniW = 250f, MiniH = 330f;

    Rect CondRect() { return condOpen ? new Rect(24, 24, 440, 740) : new Rect(24, 24, 440, 132); }
    // 브라우저에서는 태블릿을 들면 플랫폼이 화면 전체로 커진다. 실행 파일에서는 오른쪽의 태블릿 크기 그대로다
    // 플랫폼은 화면을 다 가리지 않고 오른쪽 태블릿에 띄운다. 공장과 3D 패널이 함께 보여야 플랫폼을 쓰면서 결과를 볼 수 있다
    bool FullTablet { get { return false; } }
    Rect TabletRect(float W) { return new Rect(W - 64 - TabletW, 60, TabletW, 910); }
    Rect MiniRect(float W) { return new Rect(W - 70 - MiniW, 1080 - 96 - MiniH, MiniW, MiniH); }
    Rect ProdRect(float W) { float right = tabletOpen ? 704f : (70f + MiniW + 70f); return new Rect(24, 1080 - 72 - 150, W - 24 - right, 150); } // 아래 48은 웹의 "돌아가기" 버튼 자리
    Rect StoryRect(float W) { Rect p = ProdRect(W); return new Rect(p.x, p.y - 74, p.width, 62); }

    void InitStoryStyles()
    {
        sStory = new GUIStyle(sLabel) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip };
        sStory.normal.textColor = Color.white;
        sStoryNo = new GUIStyle(sStory) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
        sMiniTitle = new GUIStyle(sHead) { fontSize = 20, alignment = TextAnchor.UpperLeft };
        sMiniText = new GUIStyle(sSmall) { fontSize = 16, wordWrap = true };
        sClear = new GUIStyle(); // 보이지 않는 버튼 (영역 전체를 누를 수 있게)
    }

    // 지금 이야기의 몇 번째 단계인가 (화면 상태에서 그때그때 읽어 낸다)
    // 브라우저에서는 태블릿이 곧 업무 플랫폼이고, 실행 파일에서는 태블릿 안의 에이전트를 쓴다
    static readonly string[] StoryWeb =
    {
        "제품에 하얀 줄이 생겼습니다. 오른쪽 아래 태블릿을 눌러 업무 플랫폼을 여세요.",
        "현장 에이전트 칸에 본 대로 적고, 나온 노하우에서 '이 순서대로 점검했어요'를 누르세요.",
        "이제 100쇼트를 생산해 보세요. 작업표준서만 볼 때와 비교됩니다.",
        "생산 중입니다. 빈 곳을 드래그해 공장을 둘러보세요.",
        "노하우를 남긴 조장님에게 활용이 쌓였습니다. 보상 원장을 열어 보세요.",
    };
    static readonly string[] StoryApp =
    {
        "제품에 하얀 줄이 생겼습니다. 오른쪽 아래 태블릿을 눌러 에이전트를 여세요.",
        "본 대로 적어 보세요. 예: 하얀 물결 같은 게 생겨요",
        "조장님이 남긴 노하우가 나왔습니다. 카드에서 '따르기'를 누르세요.",
        "이제 100쇼트를 생산해 보세요. 작업표준서만 볼 때와 비교됩니다.",
        "생산 중입니다. 빈 곳을 드래그해 공장을 둘러보세요.",
        "노하우를 남긴 조장님에게 활용이 쌓였습니다. 보상 원장을 열어 보세요.",
    };
    string[] StoryText { get { return HasWebPortal ? StoryWeb : StoryApp; } }

    int StoryStep()
    {
        int n = StoryText.Length;
        if (running) return n - 2;
        if (hasResult) return n - 1;
        bool applied = frags.Exists(f => f.applied);
        if (applied) return n - 3;
        if (HasWebPortal) return tabletOpen ? 1 : 0;
        bool asked = chat.Exists(m => m.kind == MsgKind.Me);
        if (asked) return 2;
        return tabletOpen ? 1 : 0;
    }

    void DrawStory(float W)
    {
        if (sStory == null) InitStoryStyles();
        int step = StoryStep();
        Rect r = StoryRect(W);
        Fill(r, new Color(0.05f, 0.07f, 0.10f, 0.88f));
        Fill(new Rect(r.x, r.y, 6f, r.height), Orange);
        // 단계 점 6개
        int n = StoryText.Length;
        float dx = r.xMax - 20f - n * 18f;
        for (int i = 0; i < n; i++)
            Fill(new Rect(dx + i * 18f, r.center.y - 4f, 10f, 8f), i == step ? Orange : (i < step ? new Color(1f, 1f, 1f, 0.75f) : new Color(1f, 1f, 1f, 0.25f)));
        Fill(new Rect(r.x + 22f, r.center.y - 19f, 38f, 38f), Orange);
        GUI.Label(new Rect(r.x + 22f, r.center.y - 19f, 38f, 38f), (step + 1).ToString(), sStoryNo);
        GUI.Label(new Rect(r.x + 76f, r.y, dx - r.x - 96f, r.height), StoryText[step], sStory);
    }

    // 접힌 조건 창: 지금 상황 한 줄과 예상 불량률. 누르면 펼쳐진다
    void DrawConditionsMini(Rect r)
    {
        if (sStory == null) InitStoryStyles();
        Panel(r);
        float x = r.x + 20, w = r.width - 40;
        GUI.Label(new Rect(x, r.y + 12, w - 90, 36), TacitData.PersonaName + " 씨의 야간 교대", sTitle);
        var open = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(r.xMax - 130, r.y + 14, 110, 30), "조건 바꾸기 +", open);
        SimResult std = TacitSim.Evaluate(env, frags, false);
        SimResult cur = TacitSim.Evaluate(env, frags, true);
        GUI.Label(new Rect(x, r.y + 54, w, 28), Mathf.Round(env.temp) + "℃ · 습도 " + Mathf.Round(env.humidity) + "% · 설비 " + Mathf.Round(env.age) + "년 · 근속 " + Mathf.Round(env.tenure) + "주", sSmall);
        GUI.Label(new Rect(x, r.y + 86, w, 34), "예상 불량률  " + std.defect.ToString("0.0") + "%  →  활용 시 " + cur.defect.ToString("0.0") + "%", sHead);
        if (GUI.Button(r, GUIContent.none, sClear)) condOpen = true;
    }

    // 손에 든 작은 태블릿: 누르면 들어 올려 에이전트를 연다
    void DrawTabletMini(Rect r)
    {
        if (sStory == null) InitStoryStyles();
        Fill(new Rect(r.x - 8f, r.y + 8f, r.width + 10f, r.height), new Color(0f, 0f, 0f, 0.35f));
        Fill(r, new Color(0.08f, 0.09f, 0.11f, 1f));
        Fill(new Rect(r.center.x - 3f, r.y + 5f, 6f, 6f), new Color(0.25f, 0.27f, 0.31f, 1f));
        Rect s = new Rect(r.x + 10f, r.y + 16f, r.width - 20f, r.height - 30f);
        Panel(s);
        GUI.Label(new Rect(s.x + 14f, s.y + 12f, s.width - 28f, 30f), HasWebPortal ? "업무 플랫폼" : "현장 에이전트", sMiniTitle);
        // 말풍선 모양 미리 보기
        Fill(new Rect(s.x + 14f, s.y + 54f, s.width - 60f, 36f), new Color(0.88f, 0.92f, 0.97f, 1f));
        Fill(new Rect(s.x + 46f, s.y + 100f, s.width - 60f, 36f), new Color(0.08f, 0.10f, 0.13f, 1f));
        Fill(new Rect(s.x + 14f, s.y + 146f, s.width - 80f, 36f), new Color(0.88f, 0.92f, 0.97f, 1f));
        GUI.Label(new Rect(s.x + 14f, s.y + 196f, s.width - 28f, 48f), "현장 에이전트에 본 대로 적으면 노하우를 찾아 드려요", sMiniText);
        Fill(new Rect(s.x + 14f, s.yMax - 54f, s.width - 28f, 40f), Orange);
        var go = new GUIStyle(sStory) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
        GUI.Label(new Rect(s.x + 14f, s.yMax - 54f, s.width - 28f, 40f), "눌러서 열기", go);
        // 아직 열어 보지 않았으면 알림 점이 숨 쉬듯 깜빡인다
        if (StoryStep() == 0)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            Disc(new Vector2(r.xMax - 6f, r.y + 6f), 26f + 8f * k, new Color(0.91f, 0.35f, 0.05f, 0.35f));
            Disc(new Vector2(r.xMax - 6f, r.y + 6f), 20f, Orange);
        }
        if (GUI.Button(r, GUIContent.none, sClear)) tabletOpen = true;
    }

    // 생산 띠: 공장을 가리지 않게 낮고 길게
    void DrawProductionSlim(Rect r)
    {
        Panel(r);
        float x = r.x + 22, y = r.y + 14, w = r.width - 44;
        if (running)
        {
            GUI.Label(new Rect(x, y, w, 36), "생산 중  " + shotIndex + " / " + Shots + " 쇼트  ·  불량 " + defects + "개", sTitle);
            Fill(new Rect(x, y + 52, w, 14), new Color(0, 0, 0, 0.08f));
            Fill(new Rect(x, y + 52, w * shotIndex / Shots, 14), Ink);
            if (defectLog.Count > 0) GUI.Label(new Rect(x, y + 78, w, 28), defectLog[defectLog.Count - 1], sSmall);
            return;
        }
        float bw = 230f, gap = 12f;
        if (hasResult)
        {
            int saved = stdDefects - defects;
            float tw = w - 2 * bw - 2 * gap;
            GUI.Label(new Rect(x, y, tw, 40), "생산 결과  ·  불량 " + defects + " / " + Shots + "개", sTitle);
            string cmp = "작업표준서만 봤다면 " + stdDefects + "개";
            if (saved > 0) cmp += "  →  노하우 활용으로 " + saved + "개를 막았습니다";
            GUI.Label(new Rect(x, y + 46, tw, 32), cmp, sLabel);
            GUI.Label(new Rect(x, y + 82, tw, 28), creditLine, sSmall);
            if (GUI.Button(new Rect(r.xMax - 22 - 2 * bw - gap, y + 34, bw, 52), "다시 생산", sBtn)) StartCoroutine(Run());
            if (GUI.Button(new Rect(r.xMax - 22 - bw, y + 34, bw, 52), "보상 원장 보기", sBtnOn)) showLedger = true;
        }
        else
        {
            float tw = w - bw - gap;
            GUI.Label(new Rect(x, y, tw, 40), "도어 트림 부품 " + Shots + "쇼트를 생산해 보세요", sTitle);
            GUI.Label(new Rect(x, y + 46, tw, 60), HasWebPortal ? "플랫폼에서 활용 기록을 남기지 않으면 작업표준서만 보고 생산합니다. 같은 조건에서 활용 전후를 비교해 보세요." : "노하우를 따르지 않으면 작업표준서만 보고 생산합니다. 같은 조건에서 따르기 전후를 비교해 보세요.", sWrap);
            if (GUI.Button(new Rect(r.xMax - 22 - bw, y + 34, bw, 52), Shots + "쇼트 생산 시작", sBtn)) StartCoroutine(Run());
        }
    }

    // 패널들을 한꺼번에 그린다 (OnGUI 에서 부른다)
    void DrawPanels(float W, float scale)
    {
        if (FullTablet)
        {
            // 플랫폼이 화면 전체를 쓴다. 3D 패널은 잠시 숨기고, '내려놓기'로 돌아간다
            HideWebInput();
            Rect t = TabletRect(W);
            DrawTabletPortal(t, scale);
            DrawHeldHands(heldRect);
            if (GUI.Button(new Rect(t.xMax - 170, 14, 170, 40), "내려놓기", sBtnOn)) { tabletOpen = false; SyncFromPortal(); }
            return;
        }

        if (condOpen)
        {
            Rect c = CondRect();
            DrawConditions(new Rect(c.x, c.y, c.width, c.height - 56));
            Panel(new Rect(c.x, c.yMax - 56, c.width, 56));
            if (GUI.Button(new Rect(c.x + 20, c.yMax - 50, c.width - 40, 40), "접기", sBtn)) condOpen = false;
        }
        else DrawConditionsMini(CondRect());

        DrawStory(W);
        DrawProductionSlim(ProdRect(W));

        if (tabletOpen)
        {
            // 브라우저: 태블릿에 플랫폼(현장 에이전트)이 뜬다. 실행 파일: 플랫폼을 띄울 수 없어 태블릿 안의 에이전트를 쓴다
            Rect t = TabletRect(W);
            if (HasWebPortal) DrawTabletPortal(t, scale);
            else DrawChat(t, scale);
            DrawHeldHands(heldRect);
            if (GUI.Button(new Rect(t.xMax - 150, 12, 150, 40), "내려놓기", sBtn)) { tabletOpen = false; SyncFromPortal(); }
        }
        else
        {
            HideWebInput(); // 태블릿을 내려놓으면 브라우저 입력칸도 숨긴다
            HideWebPortal();
            Rect m = MiniRect(W);
            DrawTabletMini(m);
            DrawHeldHands(m, 0.55f);
        }
    }

    // 고개 돌리기(드래그)를 막아야 하는 패널 영역인가
    bool OverPanels(Vector2 p, float W)
    {
        if (FullTablet) return true;
        if (CondRect().Contains(p)) return true;
        if (ProdRect(W).Contains(p) || StoryRect(W).Contains(p)) return true;
        if (tabletOpen)
        {
            Rect t = TabletRect(W);
            if (new Rect(t.x - 60, 0, t.width + 140, 1080).Contains(p)) return true;
        }
        else
        {
            Rect m = MiniRect(W);
            if (new Rect(m.x - 50, m.y - 20, m.width + 110, 1080 - m.y + 20).Contains(p)) return true;
        }
        return false;
    }

    // 플랫폼과 3D를 잇는다: 태블릿(플랫폼)에서 '이 순서대로 점검했어요'를 누르면 그 노하우의 활용 횟수가 올라간다.
    // 3D는 그 변화를 읽어 같은 노하우를 '따르는 중'으로 삼고, 예상 불량률과 100쇼트 생산에 반영한다.
    float nextPortalSync;
    void TickPortalSync()
    {
        if (!HasWebPortal || !tabletOpen || running || Time.time < nextPortalSync) return;
        nextPortalSync = Time.time + 1f;
        SyncFromPortal();
    }

    void SyncFromPortal()
    {
        if (!HasWebPortal) return;
        var fresh = WebStore.LoadFragments();
        if (fresh == null) return;
        bool newly = false;
        foreach (Fragment f in fresh)
        {
            Fragment old = frags.Find(o => o.id == f.id);
            if (old == null) continue; // 플랫폼에서 새로 게시된 노하우
            f.applied = old.applied;
            f.portalCredited = old.portalCredited;
            if (f.uses > old.uses) { if (!f.applied) newly = true; f.applied = true; f.portalCredited = true; }
        }
        frags = fresh;
        if (newly) { hasResult = false; Cue("follow"); }
    }

    // 태블릿 화면에 플랫폼(업무 포털)을 그대로 띄운다. 브라우저에서는 포털 화면을 이 자리에 겹쳐 놓고, 실행 파일에서는 안내만 보여 준다
    void DrawTabletPortal(Rect r, float scale)
    {
        Rect outer = r;
        outer.height += 20f;
        Fill(new Rect(outer.x - 12f, outer.y + 10f, outer.width + 14f, outer.height), new Color(0f, 0f, 0f, 0.35f));
        Fill(outer, new Color(0.08f, 0.09f, 0.11f, 1f));
        Fill(new Rect(outer.x, outer.y, 3f, outer.height), new Color(0.30f, 0.32f, 0.37f, 1f));
        Fill(new Rect(outer.center.x - 4f, outer.y + 6f, 8f, 8f), new Color(0.25f, 0.27f, 0.31f, 1f));
        Rect s = new Rect(r.x + 16f, r.y + 22f, r.width - 32f, r.height - 22f);
        Panel(s);
        heldRect = outer;
        HideWebInput();
        if (HasWebPortal) { ShowWebPortal(s, scale); return; }
        GUI.Label(new Rect(s.x + 24f, s.y + 24f, s.width - 48f, 40f), "플랫폼 전체 화면", sTitle);
        GUI.Label(new Rect(s.x + 24f, s.y + 76f, s.width - 48f, 160f), "대시보드, 노하우 라이브러리, 노하우 등록, 검토·승인, 공유·보상 원장은 웹 버전의 태블릿에서 이 자리에 그대로 열립니다.\n\n실행 파일에서는 아래 버튼으로 브라우저에서 엽니다.", sWrap);
        if (GUI.Button(new Rect(s.x + 24f, s.y + 250f, 300f, 52f), "브라우저에서 플랫폼 열기", sBtnOn)) Application.OpenURL("https://sonkkeut-ledger.vercel.app/portal.html");
    }
}
