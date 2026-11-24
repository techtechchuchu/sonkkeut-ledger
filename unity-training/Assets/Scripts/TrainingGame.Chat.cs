using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;

// 3D 화면 오른쪽의 현장 에이전트 대화 — 김민준 씨의 하룻밤.
// 에이전트가 오늘 밤 조건에 맞는 노하우를 보여 주고, 민준 씨가 본 대로 적은 증상을 알아듣고,
// 생산 결과를 알려 주고, 같은 대화에서 조장의 노하우를 새 노하우로 받는다.
// 데모의 대화는 규칙과 표현 사전으로 동작한다. 실제 서비스에서 언어 모델이 맡는 자리는 두 곳이다.
//   1) 정리: 숙련자의 30초 말을 증상 → 확인 순서 → 원인으로 정리 (IvSample이 결과를 예시로 보여 준다)
//   2) 찾기: 신입의 말을 증상으로 알아듣고 노하우를 찾음 (TacitData.MatchSymptom이 표현 사전으로 흉내 낸다)
public partial class TrainingGame
{
    enum MsgKind { Agent, Me, Note, Cards, Found, Draft }
    class ChatMsg { public MsgKind kind; public string text; }
    class Choice { public string label; public Action run; public bool main; public bool silent; }

    readonly List<ChatMsg> chat = new List<ChatMsg>();
    readonly List<Choice> choices = new List<Choice>();
    Vector2 chatScroll;
    bool chatToBottom;
    string inputText = "";

    // 숙련자 인터뷰 진행 상태
    string ivStep;          // null이면 인터뷰 중이 아님
    Fragment ivDraft;

    GUIStyle sBubble, sBubbleMe, sNote, sChip, sChipMain, sInput;
    bool chatStylesReady;

#if UNITY_WEBGL && !UNITY_EDITOR
    // 브라우저에서는 한글 입력을 위해 HTML 입력칸을 캔버스 위에 겹쳐 놓는다 (store.jslib)
    [DllImport("__Internal")] static extern void TacitInputLayout(float left, float top, float width, float height, int visible);
    Rect lastWebInput; int lastWebVisible = -1;
    void HideWebInput()
    {
        if (lastWebVisible != 0) { TacitInputLayout(0f, 0f, 0f, 0f, 0); lastWebVisible = 0; lastWebInput = new Rect(); }
    }
    [DllImport("__Internal")] static extern void TacitPortalLayout(float left, float top, float width, float height, int visible);
    Rect lastPortal; int lastPortalVisible = -1;
    // 가상 좌표(1080 기준)의 자리에 포털 화면을 겹쳐 놓는다
    void ShowWebPortal(Rect r, float scale)
    {
        var px = new Rect(r.x * scale / Screen.width, r.y * scale / Screen.height, r.width * scale / Screen.width, r.height * scale / Screen.height);
        if (px != lastPortal || lastPortalVisible != 1) { TacitPortalLayout(px.x, px.y, px.width, px.height, 1); lastPortal = px; lastPortalVisible = 1; }
    }
    void HideWebPortal()
    {
        if (lastPortalVisible == 1) { TacitPortalLayout(0f, 0f, 0f, 0f, 0); lastPortalVisible = 0; lastPortal = new Rect(); }
    }
    const bool HasWebPortal = true;
#else
    void HideWebInput() { }
    void ShowWebPortal(Rect r, float scale) { }
    void HideWebPortal() { }
    const bool HasWebPortal = false;
#endif

    // ---------- 메시지 ----------

    void Say(MsgKind kind, string text)
    {
        chat.Add(new ChatMsg { kind = kind, text = text });
        if (chat.Count > 60) chat.RemoveAt(0);
        chatToBottom = true;
    }
    void AgentSay(string text) { Say(MsgKind.Agent, text); }

    void SetChoices(params Choice[] list)
    {
        choices.Clear();
        choices.AddRange(list);
    }

    List<Fragment> Hits()
    {
        var list = new List<Fragment>();
        foreach (Fragment f in frags) if (TacitSim.Triggered(f, env)) list.Add(f);
        return list;
    }

    Fragment FindFragment(string id)
    {
        foreach (Fragment f in frags) if (f.id == id) return f;
        return null;
    }

    // ---------- 흐름 ----------

    void ChatStart()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLInput.captureAllKeyboardInput = false; // HTML 입력칸에 글자를 칠 수 있게 한다
#endif
        Input.imeCompositionMode = IMECompositionMode.On;
        AgentSay(TacitData.PersonaName + " 씨, 야간 교대입니다. " + TacitData.MentorName + "님은 퇴근했고, 사출 1라인에서 도어 트림 " + Shots + "쇼트를 찍어야 합니다.\n이상이 보이면 본 그대로 적어 주세요. 용어를 몰라도 됩니다. (예: 하얀 물결 같은 게 생겨요)");
        // 팀원들과 나누는 대화로 시작한다: 조립 A → 민준 → 정비 → 민준 → 조립 B → 민준
        for (int si = 1; si <= 6; si++) Cue("start" + si, true, 1f);
        ShowCards();
    }

    // 오늘 밤 조건에서 생기기 쉬운 증상의 노하우를 대화에 카드로 올린다. 카드는 조건이 바뀌면 함께 바뀐다
    void ShowCards()
    {
        ivStep = null;
        CueForConditions();
        chat.RemoveAll(m => m.kind == MsgKind.Cards);
        int n = Hits().Count;
        if (n > 0)
        {
            AgentSay("오늘 밤 조건에서 미리 볼 노하우가 " + n + "건 있습니다. 따를 것을 고르세요.");
            Say(MsgKind.Cards, "");
        }
        else AgentSay("오늘 밤 조건에 해당하는 노하우는 없습니다. 작업표준서대로 진행하면 됩니다.");
        ReadyChoices();
    }

    void ReadyChoices()
    {
        SetChoices(
            new Choice { label = "모두 따르기", silent = true, run = () => { foreach (Fragment f in Hits()) f.applied = true; hasResult = false; Cue("follow"); ReadyChoices(); } },
            new Choice { label = Shots + "쇼트 생산 시작", main = true, run = () => StartCoroutine(Run()) },
            new Choice { label = "상황 바꾸기", run = AskSituation },
            new Choice { label = "조장: 노하우 남기기", run = IvStart });
    }

    void AskSituation()
    {
        ivStep = null;
        AgentSay("오늘 밤 현장은 어떤가요?\n버튼을 고르거나 \"영하 3도, 습도 80%\"처럼 적어 주세요.");
        SetChoices(
            new Choice { label = "한겨울 새벽", run = () => { Preset(-3f, 35f); ShowCards(); } },
            new Choice { label = "장마철 야간", run = () => { Preset(24f, 90f); ShowCards(); } },
            new Choice { label = "열대야", run = () => { Preset(31f, 72f); ShowCards(); } });
    }

    // 왼쪽 패널의 상황 버튼을 눌렀을 때도 대화에 남긴다
    void ChatPreset(string label, float temp, float humidity)
    {
        Preset(temp, humidity);
        Say(MsgKind.Me, label);
        ShowCards();
    }

    // 증상으로 찾기: 민준 씨가 본 대로 적은 말을 증상 이름으로 잇고, 그 노하우를 보여 준다
    void ShowSymptom(string text, int symptom)
    {
        string name = TacitData.SymptomName[symptom];
        AgentSay("'" + text + "' → '" + name + "' 증상으로 이해했습니다.\n(예시 동작입니다. 데모는 표현 사전으로 찾고, 실제 서비스에서는 언어 모델이 찾습니다.)");
        var found = new List<Fragment>();
        foreach (Fragment f in frags)
            if (f.id == TacitData.SymptomRule[symptom] || f.defectType == name) found.Add(f);
        if (found.Count == 0)
        {
            AgentSay("이 증상에 대해 남겨진 노하우가 아직 없습니다. 작업표준서를 확인하고, 해결되면 조장님께 남겨 달라고 요청하세요.");
            return;
        }
        AgentSay("조장님들이 남긴 확인 순서가 " + found.Count + "건 있습니다. 조건은 건드리지 말고 아래 순서대로 확인하세요.");
        foreach (Fragment f in found) Say(MsgKind.Found, f.id);
        ReadyChoices();
    }

    void ChatRunStarted()
    {
        choices.Clear();
        int n = 0;
        foreach (Fragment f in Hits()) if (f.applied) n++;
        AgentSay(n > 0 ? "노하우 " + n + "건을 따르며 생산합니다. 공장을 보세요." : "작업표준서만 보고 생산합니다. 공장을 보세요.");
        Cue(n > 0 ? "run_with" : "run_without", true, 5f);
    }

    void ChatRunFinished(List<string> credits)
    {
        int saved = stdDefects - defects;
        string text = "생산이 끝났습니다. 불량 " + defects + " / " + Shots + "개입니다.\n작업표준서만 봤다면 " + stdDefects + "개";
        text += saved > 0 ? "였을 겁니다. 조장님들의 노하우로 " + saved + "개를 막았습니다." : "였습니다.";
        AgentSay(text);
        Cue(saved > 0 ? "end_saved" : (defects == 0 ? "end_clean" : "end_none"), true, 5f);
        if (credits.Count > 0) Say(MsgKind.Note, "활용 기록 +1: " + string.Join(", ", credits.ToArray()));
        else AgentSay("따른 노하우가 없어 활용 기록은 없습니다. 노하우를 따르고 다시 생산해 비교해 보세요.");
        SetChoices(
            new Choice { label = "다시 생산", main = true, run = () => StartCoroutine(Run()) },
            new Choice { label = "상황 바꾸기", run = AskSituation },
            new Choice { label = "조장: 노하우 남기기", run = IvStart },
            new Choice { label = "보상 원장 보기", run = ChatLedger });
    }

    void ChatLedger()
    {
        ivStep = null;
        showLedger = true;
        Cue("ledger");
        AgentSay("보상 원장을 띄웠습니다. 민준 씨가 노하우를 따를 때마다 남긴 사람의 활용 횟수가 올라가고, 그만큼 보상 지분이 커집니다.");
        ReadyChoices();
    }

    // ---------- 글 입력 ----------

    // 브라우저의 HTML 입력칸에서 보낸 글 (store.jslib가 SendMessage로 호출한다)
    public void OnWebText(string text) { Submit(text); }

    void Submit(string text)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0 || running) return;
        if (text.Length > 120) text = text.Substring(0, 120);
        Say(MsgKind.Me, text);
        if (ivStep != null)
        {
            if (ivStep == "confirm") { AgentSay("아래 버튼에서 등록 여부를 골라 주세요."); return; }
            choices.Clear();
            IvAnswer(text, null);
            return;
        }
        int symptom = TacitData.MatchSymptom(text);
        if (symptom >= 0) { ShowSymptom(text, symptom); return; }
        if (ParseConditions(text)) { hasResult = false; ShowCards(); return; }
        AgentSay("본 그대로 적어 주세요. 증상이나 조건 어느 쪽이든 됩니다.\n예: 하얀 물결 같은 게 생겨요 / 영하 3도, 습도 80%");
    }

    // 글에서 조건을 읽어 현장 조건에 반영한다. 하나라도 읽으면 true
    bool ParseConditions(string text)
    {
        bool hit = false;
        Match m = Regex.Match(text, @"습도\s*(\d+)|(\d+)\s*(%|퍼센트|프로)");
        if (m.Success)
        {
            env.humidity = Mathf.Clamp(float.Parse(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value), 20f, 95f);
            hit = true;
        }
        m = Regex.Match(text, @"(영하\s*)?(-?\d+)\s*(도|℃)");
        if (m.Success)
        {
            float t = float.Parse(m.Groups[2].Value);
            env.temp = Mathf.Clamp(m.Groups[1].Success ? -Mathf.Abs(t) : t, -10f, 40f);
            hit = true;
        }
        m = Regex.Match(text, @"(\d+)\s*년");
        if (m.Success) { env.age = Mathf.Clamp(float.Parse(m.Groups[1].Value), 0f, 35f); hit = true; }
        m = Regex.Match(text, @"(\d+)\s*주");
        if (m.Success) { env.tenure = Mathf.Clamp(float.Parse(m.Groups[1].Value), 0f, 52f); hit = true; }
        return hit;
    }

    // ---------- 숙련자 인터뷰: 증상 → 확인 순서 → 원인 → 발생 상황 ----------

    static readonly string[] FactorWords =
    {
        "습|장마|비\\s*오|비가|눅눅|꿉꿉|우천",
        "춥|추운|추위|추울|겨울|영하|쌀쌀|차가|새벽",
        "덥|더운|더위|더울|여름|폭염|뜨거|열대야",
        "낡|노후|오래|연식|마모|헐거|구형",
        "신입|초보|처음|미숙|저숙련|새로\\s*온|신참"
    };
    static readonly string[] ThresholdQuestion =
    {
        "습도가 몇 %쯤부터 그런가요?",
        "기온이 몇 도 아래로 내려가면 그런가요?",
        "기온이 몇 도를 넘으면 그런가요?",
        "설비가 몇 년쯤 됐을 때부터 그런가요?",
        "입사 몇 주차까지의 현장 근로자에게 해당하나요?"
    };
    static readonly float[] ThresholdDefault = { 70f, 10f, 30f, 15f, 24f };
    static readonly string[] ThresholdUnit = { "%", "℃", "℃", "년", "주" };

    // 30초 말하기 예시: 실제 서비스에서는 이 말을 언어 모델이 노하우 구조로 정리한다
    const string SampleSpeech = "장마철 밤에 제품에 하얀 줄 생기면 건조기부터 봐. 온도랑 시간 표준 채웠는지 보고, 원료 포대 언제 뜯었는지 확인해. 대개 원료가 습기 먹은 거야. 조건은 건드리지 말고.";

    void IvStart()
    {
        ivStep = "symptom";
        Cue("iv");
        ivDraft = new Fragment { mDefect = 0.4f, mDown = 0.2f }; // 현장 데이터가 쌓이기 전의 초기 추정값
        AgentSay("이번에는 조장님 차례입니다. 방금 해결하신 요령을 남겨 두면 야간에 " + TacitData.PersonaName + " 씨가 바로 찾아볼 수 있습니다.\n어떤 증상이었나요? 눈에 보인 그대로 말씀해 주세요.");
        SetChoices(
            new Choice { label = "30초 말하기 예시", silent = true, run = IvSample },
            new Choice { label = "그만두기", run = ShowCards });
    }

    // 말로 한 번에 남기는 경우: 정리된 결과를 예시로 보여 준다
    void IvSample()
    {
        Say(MsgKind.Me, "(말로) " + SampleSpeech);
        ivDraft.defectType = "은줄·기포";
        ivDraft.action = "건조기 온도·시간이 표준을 채웠는지 보고, 원료 포대 개봉 시간을 확인한다. 조건은 건드리지 않는다";
        ivDraft.cause = "원료가 습기를 먹음";
        ivDraft.factor = Factor.Humidity;
        ivDraft.threshold = 70f;
        AgentSay("말씀을 증상 → 확인 순서 → 원인으로 정리했습니다.\n(예시 동작입니다. 실제 서비스에서는 언어 모델이 정리하고, 조장님이 확인한 것만 등록됩니다.)");
        IvAskAuthor();
    }

    void IvAskAuthor()
    {
        ivStep = "author";
        AgentSay("원장에 기록할 ID를 알려주세요. 실명 대신 ID로만 남깁니다. (예: S-03)");
        SetChoices(
            new Choice { label = "S-01", run = () => IvAnswer("S-01", null) },
            new Choice { label = "S-02", run = () => IvAnswer("S-02", null) },
            new Choice { label = "S-03", run = () => IvAnswer("S-03", null) });
    }

    void IvAnswer(string text, Factor? picked)
    {
        if (ivStep == "symptom")
        {
            int s = TacitData.MatchSymptom(text);
            ivDraft.defectType = s >= 0 ? TacitData.SymptomName[s] : text;
            if (s >= 0) AgentSay("'" + TacitData.SymptomName[s] + "' 증상으로 정리하겠습니다.");
            ivStep = "action";
            AgentSay("그때 무엇부터 확인하셨나요? 확인한 순서대로 적어 주세요.\n(작업표준 범위 안에서 하는 조치만 적습니다.)");
        }
        else if (ivStep == "action")
        {
            ivDraft.action = text;
            ivStep = "cause";
            AgentSay("원인은 무엇이었나요?");
        }
        else if (ivStep == "cause")
        {
            ivDraft.cause = text;
            ivStep = "situation";
            AgentSay("주로 어떤 상황에서 생기나요?\n(예: 장마철 야간에 습도가 높을 때, 한겨울 새벽 첫 가동 때)");
        }
        else if (ivStep == "situation")
        {
            Factor? f = picked;
            for (int i = 0; f == null && i < FactorWords.Length; i++)
                if (Regex.IsMatch(text, FactorWords[i])) f = (Factor)i;
            if (f == null)
            {
                AgentSay("말씀하신 상황이 아래 중 어디에 가까운가요?");
                var list = new List<Choice>();
                for (int i = 0; i < TacitData.FactorLabel.Length; i++)
                {
                    Factor option = (Factor)i;
                    list.Add(new Choice { label = TacitData.FactorLabel[i], run = () => IvAnswer("", option) });
                }
                SetChoices(list.ToArray());
                return;
            }
            ivDraft.factor = f.Value;
            ivStep = "threshold";
            AgentSay("'" + TacitData.FactorLabel[(int)f.Value] + "' 상황으로 이해했습니다.\n" + ThresholdQuestion[(int)f.Value] + " 숫자로 알려주세요.");
        }
        else if (ivStep == "threshold")
        {
            int k = (int)ivDraft.factor;
            Match m = Regex.Match(text, @"-?\d+(\.\d+)?");
            float n;
            if (m.Success)
            {
                n = float.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture);
                if (text.Contains("영하") && n > 0) n = -n;
            }
            else
            {
                n = ThresholdDefault[k];
                AgentSay("숫자를 찾지 못해 우선 " + n + ThresholdUnit[k] + "로 적어 두겠습니다.");
            }
            ivDraft.threshold = n;
            IvAskAuthor();
        }
        else if (ivStep == "author")
        {
            ivDraft.author = text.Length > 12 ? text.Substring(0, 12) : text;
            ivStep = "confirm";
            Say(MsgKind.Draft, "증상: " + ivDraft.defectType + " · 주로 " + TacitData.ConditionText(ivDraft)
                + "\n확인 순서: " + ivDraft.action + "\n원인: " + ivDraft.cause + "\n원작성자 " + ivDraft.author);
            AgentSay("이렇게 정리했습니다. 원장에 등록할까요?");
            SetChoices(
                new Choice { label = "등록", main = true, run = IvRegister },
                new Choice { label = "처음부터 다시", run = IvStart });
        }
    }

    void IvRegister()
    {
        Fragment f = ivDraft;
        f.id = WebStore.NextId(frags);
        f.uses = 0;
        frags.Add(f);
        WebStore.AddRule(f);
        ivStep = null;
        Say(MsgKind.Note, f.id + " 등록 완료" + (WebStore.Linked ? " · 웹 원장에도 반영됨" : ""));
        AgentSay(TacitData.PersonaName + " 씨가 같은 증상을 만나면 이 노하우가 안내되고, 쓰일 때마다 " + f.author + "님의 활용 횟수가 올라갑니다.\n효과는 우선 추정값으로 넣었고, 실제 불량 데이터가 쌓이면 보정됩니다.");
        SetChoices(
            new Choice { label = "이 상황에서 바로 확인", main = true, run = () =>
            {
                // 방금 노하우가 해당되는 조건으로 현장을 맞춘다
                switch (f.factor)
                {
                    case Factor.Humidity: env.humidity = Mathf.Clamp(f.threshold, 20f, 95f); break;
                    case Factor.Cold: case Factor.Hot: env.temp = Mathf.Clamp(f.threshold, -10f, 40f); break;
                    case Factor.Age: env.age = Mathf.Clamp(f.threshold, 0f, 35f); break;
                    default: env.tenure = Mathf.Clamp(f.threshold, 0f, 52f); break;
                }
                hasResult = false;
                ShowCards();
            } },
            new Choice { label = "하나 더 남기기", run = IvStart },
            new Choice { label = "보상 원장 보기", run = ChatLedger });
    }

    // ---------- 그리기 ----------

    void InitChatStyles()
    {
        sBubble = new GUIStyle(sLabel) { fontSize = 18, wordWrap = true };
        sBubbleMe = new GUIStyle(sBubble);
        sBubbleMe.normal.textColor = Color.white;
        sNote = new GUIStyle(sLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
        sNote.normal.textColor = Green;
        sChip = MakeButton(new Color(0.90f, 0.93f, 0.97f), new Color(0.82f, 0.87f, 0.94f), new Color(0.12f, 0.31f, 0.55f));
        sChip.fontSize = 17;
        sChipMain = MakeButton(new Color(0.91f, 0.35f, 0.05f), new Color(0.72f, 0.26f, 0.04f), Color.white);
        sChipMain.fontSize = 17;
        sInput = new GUIStyle(GUI.skin.textField) { fontSize = 18, font = font, alignment = TextAnchor.MiddleLeft };
        sInput.padding = new RectOffset(12, 12, 0, 0);
        sInput.border = new RectOffset(0, 0, 0, 0);
        Texture2D inputBg = Solid(Color.white);
        sInput.normal.background = inputBg; sInput.hover.background = inputBg; sInput.focused.background = inputBg; sInput.active.background = inputBg;
        sInput.normal.textColor = Ink; sInput.hover.textColor = Ink; sInput.focused.textColor = Ink; sInput.active.textColor = Ink;
        chatStylesReady = true;
    }

    float BubbleWidth(ChatMsg m, float maxW, GUIStyle style)
    {
        float natural = 0f;
        foreach (string line in m.text.Split('\n'))
            natural = Mathf.Max(natural, style.CalcSize(new GUIContent(line)).x);
        return Mathf.Min(maxW, natural + 30f);
    }

    float MsgHeight(ChatMsg m, float w, int cardCount)
    {
        switch (m.kind)
        {
            case MsgKind.Note: return sNote.CalcHeight(new GUIContent(m.text), w - 80f) + 12f; // 길면 여러 줄로
            case MsgKind.Cards: return cardCount * (CardHeight + 8f);
            case MsgKind.Found: return FindFragment(m.text) != null ? CardHeight : 0f;
            case MsgKind.Draft: return sBubble.CalcHeight(new GUIContent(m.text), w - 40f) + 54f;
            default:
                GUIStyle st = m.kind == MsgKind.Me ? sBubbleMe : sBubble;
                float bw = BubbleWidth(m, w * 0.88f, st);
                return st.CalcHeight(new GUIContent(m.text), bw - 28f) + 20f;
        }
    }

    void DrawChat(Rect r, float scale)
    {
        if (!chatStylesReady) InitChatStyles();
        // 태블릿: 어두운 테두리와 카메라 점, 아래 홈 막대. 화면은 안쪽에 그린다
        Rect outer = r;
        outer.height += 20f; // 아래 테두리
        heldRect = outer; // 1인칭: 손은 맨 나중에 그린다 (OnGUI)
        Fill(new Rect(outer.x - 12f, outer.y + 10f, outer.width + 14f, outer.height), new Color(0f, 0f, 0f, 0.35f)); // 그림자
        Fill(new Rect(outer.x, outer.y, outer.width, outer.height), new Color(0.08f, 0.09f, 0.11f, 1f));
        Fill(new Rect(outer.x, outer.y, 3f, outer.height), new Color(0.30f, 0.32f, 0.37f, 1f));                    // 테두리 빛
        Fill(new Rect(outer.center.x - 4f, outer.y + 6f, 8f, 8f), new Color(0.25f, 0.27f, 0.31f, 1f));                   // 카메라
        r = new Rect(r.x + 16f, r.y + 22f, r.width - 32f, r.height - 22f);
        Panel(r);

        // 조건 밖으로 나간 노하우는 따르기도 풀린다
        foreach (Fragment f in frags) if (!TacitSim.Triggered(f, env)) f.applied = false;
        List<Fragment> hits = Hits();

        float x = r.x + 18, w = r.width - 36;
        GUI.Label(new Rect(x, r.y + 14, w, 36), "현장 에이전트", sTitle);
        GUI.Label(new Rect(x, r.y + 52, w, 30), "용어를 몰라도 본 대로 적으면 찾아 드립니다", sSmall);

        // 에이전트 안의 4가지 기능: 찾기 · 노하우 남기기 · 보상 원장 · 자료
        {
            string[] tabNames = { "노하우 찾기", "노하우 남기기", "보상 원장", "자료" };
            int cur = showDoc ? 3 : (showLedger ? 2 : (ivStep != null ? 1 : 0));
            float tw = (w - 3 * 6f) / 4f;
            GUI.enabled = !running;
            for (int ti = 0; ti < 4; ti++)
            {
                var tr = new Rect(x + ti * (tw + 6f), r.y + 88f, tw, 40f);
                var tst = ti == cur ? sBtnOn : sChip;
                if (GUI.Button(tr, tabNames[ti], tst) && ti != cur)
                {
                    showDoc = false;
                    if (ti == 0) { showLedger = false; ivStep = null; ShowCards(); }
                    else if (ti == 1) { showLedger = false; IvStart(); }
                    else if (ti == 2) ChatLedger();
                    else { showLedger = false; showDoc = true; }
                }
            }
            GUI.enabled = true;
        }

        // 아래에서부터: 입력칸, 선택 버튼, 그 위가 대화
        var inputRect = new Rect(x, r.yMax - 16 - 48, w, 48);

        // 선택 버튼을 여러 줄로 배치
        var chipRects = new List<Rect>();
        float cx = 0f, rows = choices.Count > 0 ? 1f : 0f;
        foreach (Choice c in choices)
        {
            float cw = (c.main ? sChipMain : sChip).CalcSize(new GUIContent(c.label)).x + 30f;
            if (cx > 0f && cx + cw > w) { cx = 0f; rows += 1f; }
            chipRects.Add(new Rect(cx, (rows - 1f) * 46f, cw, 40f));
            cx += cw + 8f;
        }
        float chipsH = rows * 46f;
        float chipsY = inputRect.y - 10f - chipsH;

        // 대화 영역
        var view = new Rect(x, r.y + 138, w, chipsY - 8f - (r.y + 138));
        float inner = w - 18f; // 스크롤바 자리
        float total = 0f;
        foreach (ChatMsg m in chat) total += MsgHeight(m, inner, hits.Count) + 8f;
        if (chatToBottom)
        {
            chatScroll.y = Mathf.Max(0f, total - view.height);
            if (Event.current.type == EventType.Repaint) chatToBottom = false;
        }

        Fill(view, new Color(0.96f, 0.96f, 0.94f, 1f));
        chatScroll = GUI.BeginScrollView(view, chatScroll, new Rect(0, 0, inner, Mathf.Max(total, view.height - 2f)));
        float y = 8f;
        foreach (ChatMsg m in chat)
        {
            float h = MsgHeight(m, inner, hits.Count);
            switch (m.kind)
            {
                case MsgKind.Agent:
                {
                    float bw = BubbleWidth(m, inner * 0.88f, sBubble);
                    Fill(new Rect(6, y, bw, h), new Color(0.88f, 0.92f, 0.97f, 1f));
                    GUI.Label(new Rect(20, y + 9, bw - 28, h - 16), m.text, sBubble);
                    break;
                }
                case MsgKind.Me:
                {
                    float bw = BubbleWidth(m, inner * 0.88f, sBubbleMe);
                    Fill(new Rect(inner - bw - 4, y, bw, h), new Color(0.08f, 0.10f, 0.13f, 1f));
                    GUI.Label(new Rect(inner - bw + 10, y + 9, bw - 28, h - 16), m.text, sBubbleMe);
                    break;
                }
                case MsgKind.Note:
                    Fill(new Rect(30, y, inner - 60, h), new Color(0.88f, 0.95f, 0.89f, 1f));
                    GUI.Label(new Rect(40, y, inner - 80, h), m.text, sNote);
                    break;
                case MsgKind.Cards:
                {
                    float cy = y;
                    foreach (Fragment f in hits)
                    {
                        DrawCard(new Rect(6, cy, inner - 10, CardHeight), f);
                        cy += CardHeight + 8f;
                    }
                    break;
                }
                case MsgKind.Found:
                {
                    Fragment f = FindFragment(m.text);
                    if (f != null) DrawCard(new Rect(6, y, inner - 10, CardHeight), f);
                    break;
                }
                case MsgKind.Draft:
                    Fill(new Rect(6, y, inner - 10, h), Color.white);
                    Fill(new Rect(6, y, 6, h), MachineBuilder.LampWait);
                    GUI.Label(new Rect(24, y + 6, inner - 40, 30), "등록 전 확인 · 사출 성형", sHead);
                    GUI.Label(new Rect(24, y + 40, inner - 40, h - 46), m.text, sBubble);
                    break;
            }
            y += h + 8f;
        }
        GUI.EndScrollView();

        // 선택 버튼
        Choice clicked = null;
        GUI.enabled = !running;
        for (int i = 0; i < choices.Count; i++)
        {
            Rect cr = chipRects[i];
            if (GUI.Button(new Rect(x + cr.x, chipsY + cr.y, cr.width, cr.height), choices[i].label, choices[i].main ? sChipMain : sChip))
                clicked = choices[i];
        }
        GUI.enabled = true;
        if (clicked != null)
        {
            if (!clicked.silent) Say(MsgKind.Me, clicked.label);
            clicked.run();
        }

        DrawChatInput(inputRect, scale);
    }

    // 알약 모양(양 끝이 둥근) 흰색 그림. 색은 GUI.color 로 입힌다
    Texture2D capsuleTex;
    Texture2D CapsuleTex()
    {
        if (capsuleTex != null) return capsuleTex;
        const int Wd = 128, Ht = 64;
        capsuleTex = new Texture2D(Wd, Ht, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        float rad = Ht / 2f;
        for (int y = 0; y < Ht; y++)
            for (int x = 0; x < Wd; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, rad, Wd - rad);
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - rad) * (y + 0.5f - rad));
                capsuleTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((rad - d) * 1.5f)));
            }
        capsuleTex.Apply();
        return capsuleTex;
    }

    void Capsule(Vector2 center, float length, float thick, float angle, Color c)
    {
        Matrix4x4 old = GUI.matrix;
        // RotateAroundPivot 은 축소(scale) 행렬 앞에서 중심을 잡아 위치가 어긋나므로, 가상 좌표 안에서 직접 돌린다
        GUI.matrix = old * Matrix4x4.TRS(new Vector3(center.x, center.y, 0f), Quaternion.Euler(0f, 0f, angle), Vector3.one)
                         * Matrix4x4.TRS(new Vector3(-center.x, -center.y, 0f), Quaternion.identity, Vector3.one);
        Color oc = GUI.color; GUI.color = c;
        GUI.DrawTexture(new Rect(center.x - length / 2f, center.y - thick / 2f, length, thick), CapsuleTex());
        GUI.color = oc;
        GUI.matrix = old;
    }

    // 원 모양 그림(흰색, 색은 GUI.color)
    Texture2D discTex;
    void Disc(Vector2 c, float dia, Color col)
    {
        if (discTex == null)
        {
            const int N = 64;
            discTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = x + 0.5f - N / 2f, dy = y + 0.5f - N / 2f;
                    discTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((N / 2f - Mathf.Sqrt(dx * dx + dy * dy)) * 1.5f)));
                }
            discTex.Apply();
        }
        Color oc = GUI.color; GUI.color = col;
        GUI.DrawTexture(new Rect(c.x - dia / 2f, c.y - dia / 2f, dia, dia), discTex);
        GUI.color = oc;
    }

    static readonly Color SkinC = new Color(0.90f, 0.70f, 0.56f, 1f), SkinShade = new Color(0.76f, 0.56f, 0.43f, 1f);
    static readonly Color ShirtC = new Color(0.22f, 0.27f, 0.35f, 1f), VestC = new Color(0.91f, 0.35f, 0.05f, 1f), HelmetC = new Color(0.98f, 0.80f, 0.14f, 1f);

    Rect heldRect;
    // 1인칭: 내가 태블릿을 두 손으로 들고 있다. 소매 입은 팔이 화면 아래에서 올라와 아래 모서리를 받치고, 장갑 낀 엄지·검지가 앞 테두리에 걸린다
    void DrawPersonBack(Rect t) { heldRect = t; }

    void DrawHeldHands(Rect t, float k = 1f)
    {
        Color sleeve = ShirtC, cuff = VestC;
        Color glove = new Color(0.60f, 0.63f, 0.68f, 1f), gloveDark = new Color(0.44f, 0.47f, 0.52f, 1f), gloveLight = new Color(0.72f, 0.75f, 0.79f, 1f);
        for (int side = 0; side < 2; side++)
        {
            float sg = side == 0 ? -1f : 1f;                 // -1 왼손, +1 오른손
            Vector2 p = new Vector2(side == 0 ? t.x + 2f : t.xMax - 2f, t.yMax - 22f * k); // 잡는 자리(아래 모서리 위쪽)
            Vector2 d = new Vector2(sg * 0.25f, 0.97f).normalized; // 팔이 화면 아래로 뻗는 방향
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Capsule(p + d * 250f * k, 560f * k, 112f * k, ang, sleeve);                      // 팔(작업복 소매)
            Capsule(p + d * 120f * k, 70f * k, 118f * k, ang, cuff);                         // 소매 끝 단
            Capsule(p + d * 56f * k, 140f * k, 108f * k, ang, gloveDark);                    // 손바닥(테두리 뒤를 감쌈)
            // 앞쪽 테두리에 걸친 엄지와 검지
            Vector2 inward = new Vector2(-sg, 0f);
            float tang = Mathf.Atan2(-0.80f, -sg * 0.60f) * Mathf.Rad2Deg;      // 위·안쪽을 향함
            Capsule(p + (inward * 10f + new Vector2(0f, -4f)) * k, 96f * k, 42f * k, tang, glove);      // 엄지
            Capsule(p + (inward * 18f + new Vector2(0f, -10f)) * k, 34f * k, 32f * k, tang, gloveLight); // 엄지 끝
            float iang = Mathf.Atan2(-0.95f, -sg * 0.30f) * Mathf.Rad2Deg;
            Capsule(p + (inward * 6f + new Vector2(0f, -48f)) * k, 70f * k, 32f * k, iang, glove);       // 검지
        }
    }

    Texture2D thumbTex;
    void DrawThumb(Rect r, bool left)
    {
        if (thumbTex == null)
        {
            const int N = 64;
            thumbTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x - N / 2f) / (N / 2f), dy = (y - N / 2f) / (N / 2f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) * 14f);
                    float shade = 0.80f - 0.14f * Mathf.Clamp01(d) - 0.06f * dy;
                    thumbTex.SetPixel(x, y, new Color(shade, shade, shade * 0.97f, a));
                }
            thumbTex.Apply();
        }
        GUI.DrawTexture(r, thumbTex, ScaleMode.StretchToFill, true);
    }

    void DrawChatInput(Rect r, float scale)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // HTML 입력칸을 이 자리에 맞춘다. 원장 창이 떠 있으면 숨긴다
        int visible = showLedger ? 0 : 1;
        var px = new Rect(r.x * scale / Screen.width, r.y * scale / Screen.height, r.width * scale / Screen.width, r.height * scale / Screen.height);
        if (px != lastWebInput || visible != lastWebVisible)
        {
            TacitInputLayout(px.x, px.y, px.width, px.height, visible);
            lastWebInput = px; lastWebVisible = visible;
        }
#else
        Event e = Event.current;
        bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            && GUI.GetNameOfFocusedControl() == "chatInput";
        if (enter) e.Use();
        GUI.SetNextControlName("chatInput");
        inputText = GUI.TextField(new Rect(r.x, r.y, r.width - 96, r.height), inputText, 120, sInput);
        if (string.IsNullOrEmpty(inputText) && GUI.GetNameOfFocusedControl() != "chatInput")
            GUI.Label(new Rect(r.x + 14, r.y + 9, r.width - 120, 30), "예: 하얀 물결 같은 게 생겨요", sSmall);
        bool send = GUI.Button(new Rect(r.xMax - 88, r.y, 88, r.height), "보내기", sBtn);
        if (enter || send)
        {
            string text = inputText;
            inputText = "";
            Submit(text);
        }
#endif
    }
}
