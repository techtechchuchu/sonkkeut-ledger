using UnityEngine;

// 자료정리 문서(사회적 배경, 문제 상황, 해결 방안, 기대효과)를 3D 화면 안에서 볼 수 있는 패널.
// 수치와 출처는 팀의 자료정리 문서와 웹페이지의 "사회적 배경"과 같은 내용이다.
public partial class TrainingGame
{
    bool showDoc;
    int docPage;
    Vector2 docScroll;
    GUIStyle sDocItem, sDocSrc;

    static readonly string[] DocTabs = { "사회적 배경", "문제 상황", "해결 방안", "기대효과" };

    // 각 쪽: 제목, 항목들 (항목 끝의 [출처]는 작은 글씨로 따로 그린다)
    static readonly string[][] DocItems =
    {
        new[] {
            "울산 제조업 근로자 중 50대 이상 비중이 10년 만에 29%에서 43%로 늘었어요 (전국 38%). [한국경제, 고용노동부 자료 2023년 말]",
            "울산 제조업 근로자 18만 명 중 60세 이상이 2만 1천 명, 50대가 5만 6천 명으로 광역지자체 중 가장 높아요. [한국경제]",
            "울산은 자동차 생산액 전국 1위(24.2%, 2022년)예요. [Invest Korea]",
            "뿌리산업 부족 인원이 9년 사이 2.1배(10,514명 → 22,595명)로 늘었어요. [데일리안, 뿌리산업 실태조사]",
            "뿌리산업 종사자(약 74만 명) 중 50대 이상이 36.7%, 20대는 10.3%예요. [노컷뉴스, 2025년 뿌리산업 실태조사]",
            "자동차 부품 제조업 신규채용 일자리가 6.0만 개에서 5.5만 개로 줄었어요. [다음(노컷뉴스), 2026.5]",
            "대한민국명장 평균 연령은 53.4세, 재고용제도를 운영하는 제조업체는 61.6%예요. [경기일보 2026.7.31, 한국경제]",
            "정부는 제조 AI에 투자하지만(스마트제조혁신 5조 원 이상 등), 숙련자가 노하우를 내놓을 이유는 없어요. [KDI, 산업통상부]"
        },
        new[] {
            "예시 상황: 주간조 조장은 오후부터 3호기 사출기 소리가 평소와 다르다고 느꼈지만, 인수인계 메모에는 \"특이사항 없음\"만 남았어요. 새벽 3시, 입사 4주차 신입 앞에서 사출품에 하얀 줄(은줄)이 생겨요.",
            "1. 노하우가 문서에 없어요: 작업표준서에는 조건값만 있고, 이상이 생겼을 때 무엇부터 확인하는지는 숙련자의 머릿속에만 있어요.",
            "2. 교대 때 맥락이 끊겨요: 앞 조가 느낀 이상 징후가 \"특이사항 없음\" 한 줄로 줄어 다음 조에 전달되지 않아요. (해외 공정산업 추정치는 참고 근거로만 써요) [Wolters Kluwer]",
            "3. 숫자로 남지 않는 신호가 있어요: 소리, 진동, 냄새, 색으로 이상을 알아채는 판단은 매뉴얼에도 센서 데이터에도 남지 않아요. [전자신문, 머니투데이, 뉴스1]",
            "4. 노하우를 이어받을 사람이 줄고 있어요: 숙련자가 은퇴하면 노하우도 함께 사라져요.",
            "5. 숙련자가 내놓을 이유가 없어요: 정부의 암묵지 AI 사업(480억 원)에 노동계는 \"숙련마저 빼앗긴다\"고 반발했어요. [서울신문, 다음]"
        },
        new[] {
            "1. 말로 남기는 기록: 숙련자가 문제를 해결한 직후 30초 동안 말하면 AI가 증상 → 확인 순서 → 원인 → 조치로 정리하고, 숙련자가 확인한 뒤에만 등록돼요.",
            "2. 본 그대로 찾는 안내: 신입이 \"하얀 물결 같은 게 생겨요\"처럼 말하면 노하우를 찾아 줘요. 조건을 바꾸라고 하지 않고, 무엇부터 확인할지만 알려 줘요. (오른쪽 현장 에이전트)",
            "3. 끊기지 않는 교대 인수인계 [제안]: 교대 직전 \"오늘 조정한 것\"과 \"지켜볼 것\"을 30초 말로 남기면 다음 조가 설비별 카드로 봐요.",
            "4. 감각 신호의 기록 [제안]: \"소리가 둔해졌다\", \"탄 냄새가 난다\"처럼 느낀 감각을 말 그대로 남기고, 원하면 설비 소리 10초를 녹음해요.",
            "5. 쓰일 때마다 돌아가는 보상: 모든 안내에 원작성자를 표시하고, 따른 기록을 보상 근거로 쌓아요. (아래 보상 원장)",
            "AI는 말을 정리하고 비슷한 노하우를 찾는 일만 해요. 승인 범위 판정과 보상 계산은 규칙이 맡고, 등록된 노하우가 없으면 없다고 답해요."
        },
        new[] {
            "신입 작업자: 야간·주말에도 1차 대응이 가능하고, 용어를 몰라도 검색할 수 있고, 대응 시간이 줄어요.",
            "교대조: 앞 조가 느낀 이상 징후를 이어받아 같은 문제를 반복하지 않아요.",
            "숙련자: 야간 호출이 줄고, 노하우가 쓰인 만큼 보상받고, 퇴직 후에도 기여가 인정돼요.",
            "협력사: 불량과 라인 정지 시간이 줄고, 노하우가 자산으로 남고, 신입 교육 기간이 짧아져요.",
            "데이터 자산: 소리·냄새 같은 감각 판단이 결과와 함께 쌓여 이후 이상 감지 모델의 학습 데이터가 돼요.",
            "지역·산업: 가장 빠르게 늙는 울산 제조 현장의 숙련 기술을 보존하고, 영세 협력사가 저비용으로 AI를 도입하는 사례가 돼요.",
            "사출에서 시작하지만 증상·확인 순서·원인·조치 구조는 프레스·용접 등 다른 뿌리공정으로 넓힐 수 있어요. 자동차 부품 제조업은 8,668개 사업체, 종사자 25만 700명이에요 (2025년 6월). [한국경제, 한국고용정보원]",
            "수치와 출처는 팀의 자료정리 문서 기준이에요. 기대효과는 현장 검증으로 확인할 값이에요."
        }
    };

    bool docSeen, docList;
    int docStep;
    float docStepAt, docAutoAt;
    bool docAuto = true;
    GUIStyle sDocSlide;
    bool docInit;
    float docOpenAt = -10f, docPageAt = -10f, docLastDraw = -10f;
    GUIStyle sDocBig;
    static readonly string[] StatBig = { "43%", "2.1배", "53.4세" };
    static readonly string[] StatSmall = { "울산 제조업 50대 이상", "뿌리산업 부족 인원 (9년)", "대한민국명장 평균 연령" };
    static readonly float[] StatNum = { 43f, 2.1f, 53.4f };
    static readonly float[] StatFill = { 0.43f, 0.70f, 0.76f };
    static readonly Color[] StatCol = { new Color(0.90f, 0.30f, 0.20f), new Color(0.95f, 0.62f, 0.10f), new Color(0.25f, 0.60f, 0.90f) };

    static float Ease(float t) { t = Mathf.Clamp01(t); return 1f - Mathf.Pow(1f - t, 3f); }
    static float Back(float t) { t = Mathf.Clamp01(t); float c = 1.7f; return 1f + (c + 1f) * Mathf.Pow(t - 1f, 3f) + c * Mathf.Pow(t - 1f, 2f); }

    void DrawDoc(Rect r)
    {
        if (sDocItem == null)
        {
            sDocItem = new GUIStyle(sWrap) { fontSize = 20, richText = false };
            sDocSrc = new GUIStyle(sWrap) { fontSize = 15 };
            sDocSrc.normal.textColor = new Color(0.32f, 0.33f, 0.36f);
            sDocBig = new GUIStyle(sTitle) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        }
        float now = Time.unscaledTime;
        if (now - docLastDraw > 0.25f) { docOpenAt = now; docPageAt = now; docStep = 0; docStepAt = now; docAutoAt = now; } // 새로 열린 순간부터 효과를 처음부터
        docLastDraw = now;
        float open = Back((now - docOpenAt) / 0.45f);
        Color oldC = GUI.color;
        Matrix4x4 oldM = GUI.matrix;
        Vector2 pivot = r.center;
        float sc = Mathf.Lerp(0.88f, 1f, open);
        GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), pivot);
        GUI.color = new Color(1, 1, 1, Mathf.Clamp01(open * 1.4f));

        FillA(r, new Color(0.99f, 0.99f, 0.98f, 0.95f));
        GUI.Label(new Rect(r.x + 24, r.y + 16, r.width - 200, 36), "자료정리: 왜 이 서비스가 필요한가", sTitle);
        if (GUI.Button(new Rect(r.xMax - 120, r.y + 14, 96, 38), "닫기", sBtn)) showDoc = false;

        float tw = (r.width - 48 - 3 * 8) / 4f;
        for (int i = 0; i < DocTabs.Length; i++)
            if (GUI.Button(new Rect(r.x + 24 + i * (tw + 8), r.y + 64, tw, 40), DocTabs[i], i == docPage ? sBtnOn : sBtn) && i != docPage) { docPage = i; docScroll = Vector2.zero; docPageAt = now; docStep = 0; docStepAt = now; docAutoAt = now; }
        // 선택한 탭 밑줄이 부드럽게 미끄러진다
        docBarX = Mathf.Lerp(docBarX, r.x + 24 + docPage * (tw + 8), 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        if (docBarX < 1f) docBarX = r.x + 24;
        FillA(new Rect(docBarX, r.y + 106, tw, 4), new Color(1f, 0.55f, 0.2f, 1f));
        float pt = now - docPageAt;

        float top = r.y + 118;
        if (docPage == 0)
        {
            // 핵심 수치 3개: 숫자가 올라가고 막대가 차오른다
            float cw = (r.width - 48 - 2 * 12) / 3f;
            for (int i = 0; i < 3; i++)
            {
                float e = Back((pt - 0.12f * i) / 0.5f);
                var c = new Rect(r.x + 24 + i * (cw + 12), top + (1f - Ease((pt - 0.12f * i) / 0.4f)) * 24f, cw, 100);
                float a = Mathf.Clamp01((pt - 0.12f * i) / 0.3f);
                GUI.color = new Color(1, 1, 1, a * Mathf.Clamp01(open * 1.4f));
                FillA(c, new Color(0.09f, 0.11f, 0.15f, 1f));
                FillA(new Rect(c.x, c.y, 5, c.height), StatCol[i]);
                float cnt = Ease((pt - 0.12f * i - 0.15f) / 1.1f);
                string num = i == 0 ? Mathf.RoundToInt(StatNum[i] * cnt) + "%" : i == 1 ? (StatNum[i] * cnt).ToString("0.0") + "배" : (StatNum[i] * cnt).ToString("0.0") + "세";
                sDocBig.normal.textColor = Color.Lerp(StatCol[i], Color.white, 0.4f);
                GUI.Label(new Rect(c.x + 18, c.y + 6, c.width - 24, 52), num, sDocBig);
                GUI.Label(new Rect(c.x + 18, c.y + 54, c.width - 24, 24), StatSmall[i], sDocSrc2());
                FillA(new Rect(c.x + 18, c.y + 82, c.width - 36, 6), new Color(1, 1, 1, 0.14f * a));
                FillA(new Rect(c.x + 18, c.y + 82, (c.width - 36) * StatFill[i] * Ease((pt - 0.12f * i - 0.2f) / 1.0f), 6), StatCol[i]);
            }
            top += 112;
        }
        GUI.color = new Color(1, 1, 1, Mathf.Clamp01(open * 1.4f));

        string[] items = DocItems[docPage];
        if (!docList) DrawDocSlide(r, top, items, now, open);
        else
        {
            float w = r.width - 48 - 20;
            float total = 0f;
            foreach (string it in items) total += ItemHeight(it, w) + 12f;
            var view = new Rect(r.x + 24, top, r.width - 48, r.yMax - 64 - top);
            docScroll = GUI.BeginScrollView(view, docScroll, new Rect(0, 0, w, Mathf.Max(total, view.height)));
            float y = 0f;
            int n = 0;
            foreach (string it in items)
            {
                SplitSource(it, out string body, out string src);
                float hb = sDocItem.CalcHeight(new GUIContent(body), w);
                // 항목이 차례로 오른쪽에서 밀려 들어오며 나타난다
                float delay = (docPage == 0 ? 0.35f : 0.05f) + 0.09f * n;
                float k = Ease((pt - delay) / 0.45f);
                float dx = (1f - k) * 46f;
                GUI.color = new Color(1, 1, 1, k * Mathf.Clamp01(open * 1.4f));
                GUI.Label(new Rect(dx, y, w, hb), body, sDocItem);
                y += hb;
                if (src != null)
                {
                    float hs = sDocSrc.CalcHeight(new GUIContent(src), w);
                    GUI.Label(new Rect(dx, y - 4, w, hs), src, sDocSrc);
                    y += hs - 4;
                }
                y += 12f;
                n++;
            }
            GUI.EndScrollView();
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(open * 1.4f));
    
    
        }
        if (docList && GUI.Button(new Rect(r.x + 24, r.yMax - 56, 150, 40), "한 장씩 보기", sBtn)) { docList = false; docStepAt = now; docAutoAt = now; }
        GUI.Label(new Rect(r.x + 190, r.yMax - 52, r.width - 450, 34), "출처 링크는 웹페이지의 \"사회적 배경\"에서 눌러 볼 수 있어요.", sSmall);
        if (GUI.Button(new Rect(r.xMax - 24 - 220, r.yMax - 56, 220, 40), "웹에서 출처 보기", sBtn)) Application.OpenURL(WebUrl.Replace("#demo", "#background"));
        GUI.matrix = oldM;
        GUI.color = oldC;
    }

    // 지금 GUI.color 의 투명도를 곱해 칠한다 (패널이 서서히 나타날 때 같이 투명해지도록)
    void FillA(Rect r, Color c)
    {
        Color old = GUI.color; GUI.color = new Color(c.r, c.g, c.b, c.a * old.a);
        GUI.DrawTexture(r, white);
        GUI.color = old;
    }

    // 한 번에 한 항목만 크게 보여 주고, 몇 초마다(또는 버튼으로) 다음 항목으로 넘어간다
    void DrawDocSlide(Rect r, float top, string[] items, float now, float open)
    {
        if (sDocSlide == null) sDocSlide = new GUIStyle(sWrap) { fontSize = 30, richText = false };
        float a0 = Mathf.Clamp01(open * 1.4f);
        if (docAuto && now - docAutoAt > 6f) { docStep = (docStep + 1) % items.Length; docStepAt = now; docAutoAt = now; }
        docStep = Mathf.Clamp(docStep, 0, items.Length - 1);
        float k = Ease((now - docStepAt) / 0.5f);
        float area = r.yMax - 64 - top;
        var box = new Rect(r.x + 24, top, r.width - 48, area - 52);
        // 이전 항목은 위로 사라지고 새 항목이 아래에서 올라온다
        SplitSource(items[docStep], out string body, out string src);
        float w = box.width - 60;
        float hb = sDocSlide.CalcHeight(new GUIContent(body), w);
        float y = box.y + Mathf.Max(8f, (box.height - hb - 40f) / 2f) + (1f - k) * 40f;
        GUI.color = new Color(1, 1, 1, k * a0);
        FillA(new Rect(box.x, box.y + 6, 6, box.height - 12), new Color(1f, 0.55f, 0.2f, 1f) );
        GUI.Label(new Rect(box.x + 28, y, w, hb), body, sDocSlide);
        if (src != null) GUI.Label(new Rect(box.x + 28, y + hb + 4, w, 24), src, sDocSrc);
        GUI.color = new Color(1, 1, 1, a0);
        // 아래: 이전/다음, 점 표시, 자동 재생 진행선, 목록 보기
        float by = box.yMax + 8f;
        if (GUI.Button(new Rect(r.x + 24, by, 70, 36), "이전", sBtn)) { docStep = (docStep + items.Length - 1) % items.Length; docStepAt = now; docAutoAt = now; docAuto = false; }
        if (GUI.Button(new Rect(r.x + 100, by, 70, 36), "다음", sBtn)) { docStep = (docStep + 1) % items.Length; docStepAt = now; docAutoAt = now; docAuto = false; }
        if (GUI.Button(new Rect(r.x + 176, by, 110, 36), docAuto ? "멈추기" : "자동 재생", sBtn)) { docAuto = !docAuto; docAutoAt = now; }
        float dx0 = r.x + 310f;
        for (int i = 0; i < items.Length; i++)
        {
            bool on = i == docStep;
            float dw = on ? 30f : 10f;
            FillA(new Rect(dx0, by + 14, dw, 8), on ? new Color(1f, 0.55f, 0.2f, 1f) : new Color(0.7f, 0.72f, 0.75f, 1f));
            if (on && docAuto) FillA(new Rect(dx0, by + 14, dw * Mathf.Clamp01((now - docAutoAt) / 6f), 8), new Color(0.15f, 0.15f, 0.18f, 1f));
            dx0 += dw + 6f;
        }
        if (GUI.Button(new Rect(r.xMax - 24 - 130, by, 130, 36), "목록으로 보기", sBtn)) docList = true;
    }

    float docBarX;
    GUIStyle sDocSrcLight;
    GUIStyle sDocSrc2()
    {
        if (sDocSrcLight == null) { sDocSrcLight = new GUIStyle(sDocSrc); sDocSrcLight.normal.textColor = new Color(0.80f, 0.84f, 0.90f); }
        return sDocSrcLight;
    }

    // "...문장. [출처]" 꼴에서 출처를 떼어 낸다 ([제안] 같은 짧은 표시는 본문에 남긴다)
    static void SplitSource(string text, out string body, out string src)
    {
        int i = text.LastIndexOf(" [");
        if (i > 0 && text.EndsWith("]") && text.Length - i > 6) { body = text.Substring(0, i); src = "출처: " + text.Substring(i + 2, text.Length - i - 3); }
        else { body = text; src = null; }
    }

    float ItemHeight(string text, float w)
    {
        SplitSource(text, out string body, out string src);
        float h = sDocItem.CalcHeight(new GUIContent(body), w);
        if (src != null) h += sDocSrc.CalcHeight(new GUIContent(src), w) - 4f;
        return h;
    }
}
