using UnityEngine;
using UnityEditor;

// Resources/Voice 의 대사 음성: 모노, 로드 시 압축 해제(짧은 대사라 지연 없이 재생), 브라우저에서도 같은 형식
public class VoiceImport : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.Contains("Resources/Voice")) return;
        var imp = (AudioImporter)assetImporter;
        imp.forceToMono = true;
        var s = imp.defaultSampleSettings;
        s.loadType = AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.6f;
        imp.defaultSampleSettings = s;
    }
}
