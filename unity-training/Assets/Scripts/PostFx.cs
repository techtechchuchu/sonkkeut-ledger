using UnityEngine;

// 카메라에 붙는 화면 후처리. 빛 번짐·톤 보정·비네팅·필름 입자를 입힌다.
// P키로 켜고 끌 수 있고, 기기가 느리면(평균 18프레임 아래) 자동으로 꺼서 조작이 끊기지 않게 한다.
[RequireComponent(typeof(Camera))]
public class PostFx : MonoBehaviour
{
    public static bool Active { get; private set; }

    Material mat;
    bool on;
    int frames;
    float timer, elapsed;

    void Awake()
    {
        Shader sh = Resources.Load<Shader>("FactoryPost");
        if (sh != null && sh.isSupported) mat = new Material(sh);
        on = mat != null;
        Active = on;
    }

    void Update()
    {
        if (mat == null) return;
        if (Input.GetKeyDown(KeyCode.P) && GUIUtility.keyboardControl == 0) { on = !on; Active = on; }
        // 느린 기기 보호: 시작 직후 3초는 건너뛰고, 그 뒤 5초 평균이 너무 느리면 끈다
        elapsed += Time.unscaledDeltaTime;
        if (elapsed < 3f || !on) return;
        timer += Time.unscaledDeltaTime;
        frames++;
        if (timer >= 5f)
        {
            if (timer / frames > 0.055f) { on = false; Active = false; }
            timer = 0f; frames = 0;
        }
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (!on || mat == null) { Graphics.Blit(src, dst); return; }

        mat.SetFloat("_Threshold", 0.80f);
        mat.SetFloat("_BloomIntensity", 0.70f);
        mat.SetFloat("_Vignette", 0.40f);
        mat.SetFloat("_Grain", 0.030f);
        mat.SetFloat("_Saturation", 1.08f);
        mat.SetFloat("_Contrast", 1.06f);
        mat.SetFloat("_Exposure", 1.10f);
        mat.SetFloat("_Aberration", 0.012f);
        mat.SetFloat("_Seed", Time.time % 10f);

        int w = Mathf.Max(8, src.width / 2), h = Mathf.Max(8, src.height / 2);
        RenderTexture a = RenderTexture.GetTemporary(w, h, 0, src.format);
        RenderTexture b = RenderTexture.GetTemporary(w, h, 0, src.format);
        a.filterMode = b.filterMode = FilterMode.Bilinear;
        Graphics.Blit(src, a, mat, 0);          // 밝은 부분만 뽑아 절반 크기로
        Graphics.Blit(a, b, mat, 1);            // 가로로 흐리기
        Graphics.Blit(b, a, mat, 2);            // 세로로 흐리기 → 가까운 번짐

        int w2 = Mathf.Max(4, w / 2), h2 = Mathf.Max(4, h / 2);
        RenderTexture c = RenderTexture.GetTemporary(w2, h2, 0, src.format);
        RenderTexture d = RenderTexture.GetTemporary(w2, h2, 0, src.format);
        c.filterMode = d.filterMode = FilterMode.Bilinear;
        Graphics.Blit(a, c, mat, 4);            // 한 번 더 줄여서
        Graphics.Blit(c, d, mat, 1);
        Graphics.Blit(d, c, mat, 2);            // 넓게 번지는 빛

        mat.SetTexture("_BloomTex", a);
        mat.SetTexture("_BloomTex2", c);
        Graphics.Blit(src, dst, mat, 3);        // 합성 + 톤 보정

        RenderTexture.ReleaseTemporary(a);
        RenderTexture.ReleaseTemporary(b);
        RenderTexture.ReleaseTemporary(c);
        RenderTexture.ReleaseTemporary(d);
    }
}
