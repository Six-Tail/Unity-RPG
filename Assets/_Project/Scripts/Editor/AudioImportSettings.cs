#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// Resources/Audio 아래 소리 파일의 임포트 설정(처음 임포트할 때만):
    ///   BGM/ — 길고 반복 재생: 스트리밍 · Vorbis(품질 70%) · 백그라운드 로드
    ///   SFX/ — 짧고 자주: 메모리에 풀어 두기(지연 없이 재생) · ADPCM
    /// 이미 .meta 가 있는 파일은 건드리지 않으므로 인스펙터에서 바꾼 값은 유지됩니다.
    /// </summary>
    public class AudioImportSettings : AssetPostprocessor
    {
        private const string Root = "Assets/_Project/Resources/Audio/";

        private void OnPreprocessAudio()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Root) || !assetImporter.importSettingsMissing) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            if (path.StartsWith(Root + "BGM/"))
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                ai.loadInBackground = true;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.ADPCM;
                ai.forceToMono = true;
            }
            ai.defaultSampleSettings = s;
        }
    }
}
#endif
