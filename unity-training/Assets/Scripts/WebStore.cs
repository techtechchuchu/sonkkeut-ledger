using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

// 웹페이지(agent-web)와 같은 브라우저 저장소를 쓴다.
// 웹에서 숙련자가 등록한 노하우가 훈련 장면에 나타나고, 훈련에서 따른 기록이 웹 보상 원장에 반영된다.
// 브라우저가 아닌 실행 환경(Windows 실행 파일, 에디터)에서는 아무것도 하지 않는다.
public static class WebStore
{
    const string Key = "tacit-agent-v2"; // agent-web/app.js의 LS_KEY와 같아야 한다

    [Serializable]
    class StoredRule
    {
        public string id, process, factor, action, defectType, cause, author;
        public float threshold, mDefect, mDown;
        public int uses;
        public bool active = true;
    }

    [Serializable]
    class StoredList { public List<StoredRule> items = new List<StoredRule>(); }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern string TacitGetStore(string key);
    [DllImport("__Internal")] static extern void TacitSetStore(string key, string value);
#else
    static string TacitGetStore(string key) { return ""; }
    static void TacitSetStore(string key, string value) { }
#endif

    static StoredList stored; // 웹 저장소의 전체 노하우(다른 공정 포함). 없으면 null

    static bool TryFactor(string name, out Factor f)
    {
        switch (name)
        {
            case "humidity": f = Factor.Humidity; return true;
            case "cold": f = Factor.Cold; return true;
            case "hot": f = Factor.Hot; return true;
            case "age": f = Factor.Age; return true;
            case "skill": f = Factor.Skill; return true;
        }
        f = Factor.Humidity;
        return false;
    }

    // 웹 저장소에 사출 공정 노하우가 있으면 돌려준다. 없으면 null (호출한 쪽이 기본 노하우를 쓴다)
    public static List<Fragment> LoadFragments()
    {
        try
        {
            string json = TacitGetStore(Key);
            if (string.IsNullOrEmpty(json)) return null;
            stored = JsonUtility.FromJson<StoredList>("{\"items\":" + json + "}");
            var result = new List<Fragment>();
            foreach (StoredRule r in stored.items)
            {
                Factor f;
                if (r.process != "injection" || !r.active || !TryFactor(r.factor, out f)) continue;
                result.Add(new Fragment
                {
                    id = r.id, factor = f, threshold = r.threshold, action = r.action, defectType = r.defectType,
                    cause = string.IsNullOrEmpty(r.cause) ? TacitData.DefaultCause(r.id) : r.cause,
                    mDefect = r.mDefect, mDown = r.mDown, author = r.author, uses = r.uses
                });
            }
            if (result.Count == 0) { stored = null; return null; }
            return result;
        }
        catch (Exception e)
        {
            Debug.LogWarning("웹 저장소를 읽지 못해 기본 노하우를 씁니다: " + e.Message);
            stored = null;
            return null;
        }
    }

    // 훈련에서 올라간 활용 횟수를 웹 저장소에 반영한다
    public static void SaveUses(List<Fragment> frags)
    {
        if (stored == null) return;
        try
        {
            foreach (Fragment f in frags)
                foreach (StoredRule r in stored.items)
                    if (r.id == f.id) r.uses = f.uses;
            var parts = new List<string>();
            foreach (StoredRule r in stored.items) parts.Add(JsonUtility.ToJson(r));
            TacitSetStore(Key, "[" + string.Join(",", parts.ToArray()) + "]");
        }
        catch (Exception e)
        {
            Debug.LogWarning("웹 저장소에 쓰지 못했습니다: " + e.Message);
        }
    }

    public static bool Linked { get { return stored != null; } }

    static readonly string[] FactorNames = { "humidity", "cold", "hot", "age", "skill" };

    // 새 노하우의 번호. 웹 저장소가 있으면 다른 공정의 노하우 번호와도 겹치지 않게 한다
    public static string NextId(List<Fragment> frags)
    {
        int max = 0;
        foreach (Fragment f in frags) max = Mathf.Max(max, IdNumber(f.id));
        if (stored != null) foreach (StoredRule r in stored.items) max = Mathf.Max(max, IdNumber(r.id));
        return "TF-" + (max + 1).ToString("000");
    }

    static int IdNumber(string id)
    {
        int n = 0;
        foreach (char c in id ?? "") if (c >= '0' && c <= '9') n = n * 10 + (c - '0');
        return n;
    }

    // 3D 화면의 인터뷰로 등록한 노하우를 웹 저장소에도 넣는다
    public static void AddRule(Fragment f)
    {
        if (stored == null) return;
        try
        {
            stored.items.Add(new StoredRule
            {
                id = f.id, process = "injection", factor = FactorNames[(int)f.factor], threshold = f.threshold,
                action = f.action, defectType = f.defectType, cause = f.cause ?? "", mDefect = f.mDefect, mDown = f.mDown,
                author = f.author, uses = f.uses, active = true
            });
            var parts = new List<string>();
            foreach (StoredRule r in stored.items) parts.Add(JsonUtility.ToJson(r));
            TacitSetStore(Key, "[" + string.Join(",", parts.ToArray()) + "]");
        }
        catch (Exception e)
        {
            Debug.LogWarning("웹 저장소에 노하우를 넣지 못했습니다: " + e.Message);
        }
    }
}
