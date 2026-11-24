using System.Collections.Generic;
using UnityEngine;

public struct Env
{
    public float temp;      // 외기온도(℃)
    public float humidity;  // 습도(%)
    public float age;       // 설비 사용연수(년)
    public float tenure;    // 작업자 근속(주)
}

public struct SimResult
{
    public float defect;      // 불량률(%)
    public float down;        // 월 다운타임(h)
    public float[] contrib;   // 요인별 불량 기여(%p)
}

// 웹 대시보드의 sim.js와 같은 식.
// 불량률 = 기준 × (1 + Σ 가중치 × 요인 강도 × (1 − 적용된 노하우의 완화율))
public static class TacitSim
{
    public static float[] Factors(Env e)
    {
        return new[]
        {
            Mathf.Clamp01((e.humidity - 50f) / 40f),
            Mathf.Clamp01((15f - e.temp) / 20f),
            Mathf.Clamp01((e.temp - 25f) / 15f),
            Mathf.Clamp01(e.age / 25f),
            Mathf.Clamp01(1f - e.tenure / TacitData.OnboardWeeks)
        };
    }

    public static bool Triggered(Fragment f, Env e)
    {
        switch (f.factor)
        {
            case Factor.Humidity: return e.humidity >= f.threshold;
            case Factor.Cold: return e.temp <= f.threshold;
            case Factor.Hot: return e.temp >= f.threshold;
            case Factor.Age: return e.age >= f.threshold;
            default: return e.tenure <= f.threshold;
        }
    }

    // useApplied = false 이면 표준 매뉴얼만 따른 경우
    public static SimResult Evaluate(Env e, List<Fragment> frags, bool useApplied)
    {
        float[] f = Factors(e);
        float[] keepDef = { 1, 1, 1, 1, 1 };
        float[] keepDown = { 1, 1, 1, 1, 1 };
        if (useApplied)
        {
            foreach (Fragment fr in frags)
            {
                if (!fr.applied || !Triggered(fr, e)) continue;
                keepDef[(int)fr.factor] *= 1f - fr.mDefect;
                keepDown[(int)fr.factor] *= 1f - fr.mDown;
            }
        }
        var r = new SimResult { contrib = new float[5], defect = TacitData.BaseDefect };
        float downMul = 1f;
        for (int i = 0; i < 5; i++)
        {
            r.contrib[i] = TacitData.BaseDefect * TacitData.DefectWeight[i] * f[i] * keepDef[i];
            r.defect += r.contrib[i];
            downMul += TacitData.DownWeight[i] * f[i] * keepDown[i];
        }
        r.defect = Mathf.Min(100f, r.defect);
        r.down = TacitData.BaseDown * downMul;
        return r;
    }

    // 불량이 난 쇼트의 유형을 기여도 비율로 고른다. u는 0~1 난수
    public static string PickDefectType(SimResult r, float u)
    {
        float total = TacitData.BaseDefect;
        for (int i = 0; i < 5; i++) total += r.contrib[i];
        float x = u * total;
        for (int i = 0; i < 5; i++)
        {
            if (x < r.contrib[i]) return TacitData.FactorDefect[i];
            x -= r.contrib[i];
        }
        return "기타(이물·흑점)";
    }
}
