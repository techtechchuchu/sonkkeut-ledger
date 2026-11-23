using System.Collections.Generic;
using System.Text.RegularExpressions;

// 위험 요인. 배열 인덱스로도 쓴다.
public enum Factor { Humidity = 0, Cold = 1, Hot = 2, Age = 3, Skill = 4 }

// 암묵지 조각: 증상 → 확인 순서 → 원인. 주로 생기는 상황(요인·임계값)과 원작성자, 활용 횟수가 함께 기록된다
public class Fragment
{
    public string id;
    public Factor factor;   // 주로 생기는 상황
    public float threshold;
    public string defectType; // 증상
    public string action;     // 확인 순서 (작업표준 범위 안의 조치)
    public string cause;      // 원인
    public float mDefect;   // 해당 요인의 불량 기여분을 줄이는 비율(가정)
    public float mDown;     // 해당 요인의 다운타임 기여분을 줄이는 비율(가정)
    public string author;   // 원작성 숙련자 ID
    public int uses;        // 현장 활용 횟수
    public bool applied;    // 이번 훈련에서 작업자가 따르기로 했는가
}

// 사출 성형 공정의 예시 가정값. 실제 현장 데이터로 보정해야 한다.
public static class TacitData
{
    // 이야기의 주인공과 상대역 (가상 인물)
    public const string PersonaName = "김민준";
    public const string MentorName = "박 조장";

    public const float BaseDefect = 2.5f;   // 기준 불량률(%)
    public const float BaseDown = 14f;      // 기준 월 다운타임(h)
    public const float OnboardWeeks = 24f;  // 독립 작업까지 기간(주)

    // 요인별 가중치: 고습, 저온, 고온, 설비 노후, 저숙련
    public static readonly float[] DefectWeight = { 0.9f, 0.5f, 0.4f, 0.8f, 1.2f };
    public static readonly float[] DownWeight = { 0.2f, 0.3f, 0.2f, 1.0f, 0.9f };

    public static readonly string[] FactorLabel = { "고습", "저온", "고온", "설비 노후", "저숙련" };
    public static readonly string[] FactorDefect = { "은줄·기포", "웰드라인·미성형", "휨·변형", "수축·중량 편차", "임의 조정으로 인한 불량" };

    // 증상 표현 사전: 신입이 용어를 몰라도 본 대로 말하면 증상 이름으로 잇는다.
    // 데모는 이 사전으로 동작하고, 실제 서비스에서는 언어 모델이 이 일을 맡는다. (웹의 data.js symptoms와 같은 내용)
    public static readonly string[] SymptomName = { "은줄·기포", "웰드라인·미성형", "휨·변형", "수축·중량 편차", "임의 조정 불량" };
    public static readonly string[] SymptomRule = { "TF-001", "TF-002", "TF-003", "TF-004", "TF-005" };
    static readonly string[] SymptomWords =
    {
        "하얀\\s*줄|하얀\\s*물결|흰\\s*줄|은색\\s*줄|줄무늬|물결|기포|뿌옇|은줄",
        "선이\\s*생|실금|금이\\s*간|갈라진|덜\\s*찼|안\\s*찼|모자라|끝이\\s*안|미성형|웰드",
        "휘|뒤틀|틀어|변형|안\\s*맞|들뜨",
        "움푹|꺼졌|꺼진|패였|패인|싱크|수축|가벼|무게",
        "계속\\s*불량|뭘\\s*해야|모르겠|어떻게\\s*해"
    };

    // 말에서 증상을 찾는다. 없으면 -1
    public static int MatchSymptom(string text)
    {
        for (int i = 0; i < SymptomWords.Length; i++)
            if (Regex.IsMatch(text ?? "", SymptomWords[i])) return i;
        return -1;
    }

    // 가상의 숙련자(익명 ID). 실제 인물 아님
    public static readonly Dictionary<string, string> Experts = new Dictionary<string, string>
    {
        { "S-01", "박 조장 · 사출 27년차" },
        { "S-02", "최 반장 · 금형 셋업 19년차" }
    };

    // 웹에서 새로 등록한 숙련자는 목록에 없을 수 있다
    public static string ExpertLabel(string id)
    {
        string label;
        return Experts.TryGetValue(id ?? "", out label) ? label : "신규 등록 숙련자";
    }

    public static List<Fragment> CreateFragments()
    {
        return new List<Fragment>
        {
            new Fragment { id = "TF-001", factor = Factor.Humidity, threshold = 65,
                defectType = "은줄·기포",
                action = "원료 개봉 시간과 건조기 온도·시간이 표준을 채웠는지 확인한 뒤 투입한다",
                cause = "원료가 습기를 먹음(건조 부족)",
                mDefect = 0.6f, mDown = 0.3f, author = "S-01", uses = 41 },
            new Fragment { id = "TF-002", factor = Factor.Cold, threshold = 10,
                defectType = "웰드라인·미성형",
                action = "금형 온도가 표준 범위에 오를 때까지 기다리고, 초기 10쇼트는 폐기 후 초물 검사한다",
                cause = "금형이 덜 데워져 수지가 빨리 굳음",
                mDefect = 0.55f, mDown = 0.25f, author = "S-02", uses = 28 },
            new Fragment { id = "TF-003", factor = Factor.Hot, threshold = 30,
                defectType = "휨·변형",
                action = "냉각수 입·출구 온도 차를 먼저 보고, 벌어졌으면 칠러 필터와 유량을 점검한다",
                cause = "냉각이 고르지 않음(냉각수 온도 차)",
                mDefect = 0.5f, mDown = 0.2f, author = "S-02", uses = 12 },
            new Fragment { id = "TF-004", factor = Factor.Age, threshold = 15,
                defectType = "수축·중량 편차",
                action = "중량이 흔들리면 조건을 건드리기 전에 쿠션량을 보고, 보전 담당에게 체크링 점검을 요청한다",
                cause = "체크링 마모로 계량이 흔들림",
                mDefect = 0.5f, mDown = 0.4f, author = "S-01", uses = 36 },
            new Fragment { id = "TF-005", factor = Factor.Skill, threshold = 24,
                defectType = "임의 조정 불량",
                action = "불량이 나면 조건을 바꾸지 말고 원료 → 금형 온도 → 설비 순으로 확인해 기록한다",
                cause = "원인을 모른 채 조건을 건드려 불량이 번짐",
                mDefect = 0.35f, mDown = 0.45f, author = "S-01", uses = 53 }
        };
    }

    // 기본 조각의 원인 (웹 저장소의 예전 조각에는 원인이 없을 수 있다)
    public static string DefaultCause(string id)
    {
        foreach (Fragment f in CreateFragments()) if (f.id == id) return f.cause;
        return "";
    }

    public static string ConditionText(Fragment f)
    {
        switch (f.factor)
        {
            case Factor.Humidity: return "습도 " + f.threshold.ToString("0") + "% 이상";
            case Factor.Cold: return "외기온도 " + f.threshold.ToString("0") + "℃ 이하";
            case Factor.Hot: return "외기온도 " + f.threshold.ToString("0") + "℃ 이상";
            case Factor.Age: return "설비 사용 " + f.threshold.ToString("0") + "년 이상";
            default: return "근속 " + f.threshold.ToString("0") + "주 이하";
        }
    }
}
