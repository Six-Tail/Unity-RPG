# 움직이는 일러스트 — 반복 영상 · Live2D (6성 캐릭터)

6성 캐릭터의 일러스트 자리(홈 픽업 칸 · 가챠 액자 · 도감 상세/크게 보기 · 가챠 6성 등장 연출)에는 정적 그림 대신 움직이는 일러스트가 나옵니다.
찾는 순서: **① Live2D 모델 → ② 반복 영상 → ③ 정적 일러스트** ([Live2DUI](../Assets/_Project/Scripts/UI/Live2DUI.cs)).

## 반복 영상 (지금 쓰는 방식)

일러스트 한 장으로 만든 짧은 영상을 끊김 없이 반복 재생합니다. Cubism Editor 작업 없이 바로 만들 수 있습니다.

- 넣는 곳: `Assets/_Project/Resources/Art/Motion/{캐릭터 id}.mp4` — 이 이름으로 넣으면 자동 연결됩니다.
- 재생: [MotionVideoStage](../Assets/_Project/Scripts/Characters/Live2D/MotionVideoStage.cs)가 `VideoPlayer`로 RenderTexture에 재생해 일러스트 자리에 표시합니다. 영상이 준비되기 전에는 정적 일러스트를 깔아 둡니다.
- 배경까지 포함된 사각 영상입니다(투명 배경 아님). 일러스트와 같은 구도라 칸 안에서 그대로 바뀝니다.

### 만들기

AI 이미지 스튜디오의 로컬 영상 모델(**Wan 2.2** · 무료)로 일러스트 → 3초 영상을 만들고, 앞으로 재생한 뒤 되감는 식으로 이어 붙여 6초 반복 영상으로 만듭니다.

```bash
# 스튜디오 폴더에서 서버를 켠 뒤(node server.js), 이 저장소 폴더에서
node tools/art/generate-motion.js --list                                   # 6성 중 무엇이 없는지
node tools/art/generate-motion.js --studio "<스튜디오 폴더>"                # 아직 없는 6성 전부 (한 명에 3분쯤)
node tools/art/generate-motion.js --studio "<스튜디오 폴더>" --key c_ciel --force   # 한 명만 다시
```

- 움직임 지시는 **머리카락 · 옷자락 · 이펙트만 살짝 움직이고, 손과 얼굴은 고정 · 눈 깜빡임 없음**입니다.
  처음 시험에서 펼친 손이 뭉개지고 눈을 깜빡인 뒤 눈 색이 달라지는 문제가 있어 이렇게 정했습니다(`--motion "<문장>"`으로 바꿀 수 있음).
- 결과는 매번 달라집니다. 어색하면 그 캐릭터만 `--force`로 다시 만드세요.
- 영상(mp4)은 Git LFS로 올라갑니다. 한 개에 4MB쯤.

---

# Live2D (Cubism 모델)

6성 캐릭터는 반복 영상 대신 **Live2D(Cubism) 모델**로도 표시할 수 있습니다(모델이 있으면 영상보다 우선).
게임 쪽 연결은 끝나 있어서, **SDK를 넣고 모델 폴더를 정해진 위치에 두면** 화면에 움직이는 모델이 나옵니다.
모델이 없는 캐릭터는 지금까지처럼 정적 일러스트로 나옵니다(일부만 넣어도 됨).

> **확인된 것**: Cubism 5 SDK for Unity R6.1 + SDK 샘플 모델(Mao)을 `Live2D/c_seraphine/`에 넣고 자동 시험을 돌려,
> 모델이 자동 연결되고 무대 텍스처에 투명 배경으로 그려지며 대기 모션이 재생되는 것을 확인했습니다.
> **아직 없는 것**: 우리 캐릭터의 모델 파일(Cubism Editor에서 제작). 모델이 없는 캐릭터는 반복 영상(없으면 정적 일러스트)으로 나옵니다.
> 시험에 쓴 샘플 모델 폴더는 지웠습니다.

## 프로젝트 요구 사항 (이미 적용됨)

Cubism 5 SDK R5 이후는 **URP 전용**입니다(빌트인 파이프라인 미지원). 그래서 프로젝트를 URP로 씁니다.

| 항목 | 값 |
|---|---|
| 패키지 | `com.unity.render-pipelines.universal` 17.3.0 · `com.unity.inputsystem` 1.20.1 (에디터 내장) |
| 모듈 | `com.unity.modules.animation` · `com.unity.modules.audio` |
| 파이프라인 에셋 | `Assets/_Project/Settings/GameURP.asset` — 렌더러 `GameURP_Renderer.asset`(SDK의 Cubism 렌더러를 복사, Cubism 렌더 패스 포함) |
| 입력 방식 | Player Settings > Active Input Handling = **Both** (UI가 예전 입력 방식을 쓰므로 Input System만으로 바꾸면 클릭이 안 됩니다) |

- 파이프라인 설정이 비어 있으면 에디터가 로드될 때 [RenderPipelineSetup](../Assets/_Project/Scripts/Editor/RenderPipelineSetup.cs)이 자동으로 맞춥니다. 수동: 메뉴 `SubcultureRPG > Live2D > URP 설정 적용`.
- **SDK(`Assets/Live2D/`)는 저장소에 올리지 않습니다**(`.gitignore`). Cubism Core가 재배포 제한 라이선스라서입니다. 저장소를 새로 받은 PC에서는 아래 1번대로 SDK를 임포트하세요. SDK가 없어도 게임은 컴파일·실행되고 Live2D만 나오지 않습니다.

## 준비 순서

### 1. Cubism SDK for Unity 임포트

1. <https://www.live2d.com/sdk/download/unity/> 에서 **Cubism SDK for Unity**를 내려받습니다(라이선스 동의 필요 — 개인·소규모는 무료, 조건은 사이트에서 확인).
2. Unity 메뉴 **Assets > Import Package > Custom Package…** 로 받은 `.unitypackage`를 임포트합니다(전부 선택).
3. 콘솔에 컴파일 에러가 없는지 확인합니다.

SDK가 요구하는 패키지·모듈·URP 설정은 위 표대로 프로젝트에 이미 들어 있습니다.
URP를 지원하지 않는 예전 SDK(5-r.4.1 이하)는 이 프로젝트 설정과 맞지 않습니다.

### 2. 모델 만들기 (캐릭터마다)

AI 일러스트는 **한 장짜리 그림**이라 그대로는 Live2D가 되지 않습니다.

1. 일러스트를 부위별 **레이어로 나눈 PSD**를 만듭니다(머리카락 앞·옆·뒤, 눈·눈썹·입, 얼굴, 몸, 팔, 옷, 소품 …). 가려진 부분은 덧그려야 합니다.
2. **Live2D Cubism Editor**에서 PSD를 불러와 메시·디포머·파라미터(각도, 눈 깜빡임, 호흡, 머리카락 흔들림 등)를 리깅합니다.
3. 대기 모션을 하나 이상 만듭니다. **파일 이름에 `idle`을 넣으면** 게임이 대기 모션으로 고릅니다(예: `idle.motion3.json`).
4. **파일 > 임베디드 파일 내보내기 > moc3 파일 내보내기**(SDK for Unity 대상)로 내보냅니다.
   내보낸 폴더에는 `{이름}.model3.json`, `{이름}.moc3`, 텍스처 폴더, (있으면) `{이름}.physics3.json`, `motions/*.motion3.json`이 들어 있습니다.

### 3. 모델 폴더 넣기

내보낸 폴더를 **캐릭터 id 이름으로** 아래 위치에 둡니다.

```
Assets/_Project/Art/Characters/Live2D/
├── c_seraphine/        ← 폴더 이름 = 캐릭터 id
│   ├── c_seraphine.model3.json
│   ├── c_seraphine.moc3
│   ├── c_seraphine.2048/texture_00.png
│   ├── c_seraphine.physics3.json
│   └── motions/idle.motion3.json
├── c_ciel/
└── …
```

| 6성 캐릭터 | id |
|---|---|
| 세라핀 · 시엘 | `c_seraphine` · `c_ciel` |
| 아크 · 오로라 · 카엘 · 바이올렛 · 에스텔 | `c_ark` · `c_aurora` · `c_kael` · `c_violet` · `c_estelle` |

Unity로 돌아오면:

1. Cubism SDK가 `.model3.json`에서 **프리팹**을, `.motion3.json`에서 **애니메이션 클립**을 만듭니다.
2. [Live2DLinker](../Assets/_Project/Scripts/Editor/Live2DLinker.cs)가 그 프리팹과 대기 모션을 캐릭터 에셋(`live2DModelPrefab` · `live2DIdleMotion`)에 **자동으로 연결**합니다.

연결 상태 확인: 메뉴 **`SubcultureRPG > Live2D > 모델 연결 (폴더 → 캐릭터)`** — SDK 설치 여부, 연결된 캐릭터, 폴더는 있는데 프리팹이 없는 캐릭터, 폴더가 없는 6성을 보여 줍니다.

### 먼저 샘플 모델로 시험하기 (권장)

모델을 만들기 전에 연결이 되는지 보려면, SDK에 들어 있는 **샘플 모델**(예: `Assets/Live2D/Cubism/Samples/Models/` 아래 Koharu 등) 폴더를 통째로 복사해
`Assets/_Project/Art/Characters/Live2D/c_seraphine/`로 붙여 넣으면 됩니다. 홈의 픽업 칸에 세라핀 대신 샘플 모델이 움직이면 성공입니다.
(샘플 모델은 시험용입니다 — 커밋하지 말고, 확인 후 폴더를 지우세요.)

### 자동 시험

메뉴 **`SubcultureRPG > Live2D > 표시 시험 (세라핀)`** — 메인 씬을 플레이 모드로 돌려 세라핀의 Live2D 모델을 무대에 올리고,
무대가 그린 화면 두 장(`frame_a.png` · `frame_b.png`)과 보고서(`report.txt`)를 `Temp/Live2DSelfTest/`에 저장한 뒤 플레이를 끝냅니다.
보고서의 `coverage`(그려진 픽셀 비율)가 0%면 모델이 그려지지 않은 것이고, 두 장이 같으면 모션이 재생되지 않은 것입니다.
배치 모드로도 돌릴 수 있습니다([Live2DSelfTest](../Assets/_Project/Scripts/Editor/Live2DSelfTest.cs) 주석 참고).

## 어디에 나오나

Live2D 모델이 연결된 캐릭터는 아래 자리에서 정적 일러스트 대신 모델이 나옵니다.

| 화면 | 자리 |
|---|---|
| 홈 | 픽업 캐릭터 칸 |
| 도감 | 상세의 일러스트(보유한 캐릭터만) · `일러스트 크게 보기` |
| 가챠 결과 | 태생 6성 등장 연출 |

목록 칸의 작은 썸네일(육성 · 편성 · 도감 목록 · 전투 유닛)은 계속 정적 썸네일입니다.

## 동작 방식

이 게임의 화면은 전부 화면을 덮는 UI(캔버스)라, 월드 공간에 놓이는 Live2D 모델은 그대로는 가려져 보이지 않습니다. 그래서:

1. [Live2DStage](../Assets/_Project/Scripts/Characters/Live2D/Live2DStage.cs)가 모델을 화면 밖 먼 좌표에 놓고, 전용 카메라로 **RenderTexture**(768×1024)에 그립니다.
2. [Live2DUI](../Assets/_Project/Scripts/UI/Live2DUI.cs)가 그 텍스처를 일러스트 자리의 `RawImage`에 표시합니다.
3. 대기 모션은 `Playables`로 반복 재생합니다(클립의 반복 설정이 꺼져 있어도 반복). 프리팹에 Animator Controller를 직접 달았으면 그쪽이 우선합니다.
4. 표시하는 화면이 모두 꺼지면 1초 뒤 모델과 카메라를 정리합니다(한 번에 한 모델만 올라감).

Cubism SDK 타입을 코드에서 직접 참조하지 않으므로 **SDK가 없어도 프로젝트는 컴파일됩니다.**

화면 연결 방법(새 화면에 Live2D를 넣을 때):

```csharp
bool live = Live2DUI.Show(portraitImage, character);   // 모델이 있으면 그 자리에 표시
portraitImage.enabled = !live && sprite != null;       // 없으면 정적 일러스트
```

## 조정

| 무엇을 | 어디서 |
|---|---|
| 화면에 잡히는 범위(전신 → 상반신 등) | 모델 프리팹에 **`Live2D Framing`** 컴포넌트 추가 — `zoom`(1 = 전체), `offset`(y 0.2 = 키의 20% 위) |
| 텍스처 크기 · 여백 | 씬의 `Live2DStage` 오브젝트(실행 중 자동 생성, 씬에 미리 두면 그 설정 사용) |
| 가장자리가 어둡거나 밝게 뜸 | `Live2DStage`의 `premultipliedAlpha` 끄기/켜기 |
| 눈 깜빡임 | 모델에 눈 깜빡임 파라미터가 있으면 자동 입력 컴포넌트를 붙입니다(`autoEyeBlink`) |
| 대기 모션 바꾸기 | 캐릭터 에셋의 `live2DIdleMotion`에 다른 클립 지정(다시 연결하면 자동 선택으로 되돌아감) |

## 문제 해결

- **모델이 안 나오고 정적 그림 그대로** → 연결 메뉴로 상태 확인. 폴더 이름이 캐릭터 id와 같은지, 프리팹이 만들어졌는지(SDK 임포트 여부) 봅니다.
- **모델은 나오는데 안 움직임** → 대기 모션이 연결됐는지(캐릭터 에셋의 `live2DIdleMotion`) 확인. 모션이 없으면 물리·눈 깜빡임만 움직입니다.
- **너무 작게/잘려 보임** → `Live2D Framing`으로 `zoom` · `offset` 조정.
- **SDK 임포트 후 컴파일 에러** → `Packages/manifest.json`에 URP · Input System 패키지와 animation · audio 모듈이 있는지 봅니다(위 표).
- **모델이 분홍색이거나 아무것도 안 그려짐** → 그래픽 설정의 파이프라인이 `GameURP`인지, 렌더러에 `CubismRenderPassFeature`가 있는지 확인. 메뉴 `URP 설정 적용`으로 복구합니다.
- **UI 클릭이 안 됨** → Player Settings의 Active Input Handling이 `Both`인지 확인합니다.

## 참고

- 모델 파일(`.moc3` · 텍스처)은 Git LFS로 올라갑니다(`.gitattributes`).
- 무대 텍스처는 알파가 곱해진(premultiplied) 색으로 그려지는 것을 확인했습니다 → UI에는 전용 셰이더(`Resources/Shaders/UI-Premultiplied`)로 합성합니다.
- 매출 1,000만 엔 이상 사업자는 Cubism SDK 릴리스 라이선스 계약이 필요합니다(SDK의 `LICENSE.md`).
- 오디오 모듈을 켜면서 씬에 `AudioListener`가 필요해져, 실행 시 없으면 메인 카메라에 자동으로 붙입니다([AudioBootstrap](../Assets/_Project/Scripts/Core/AudioBootstrap.cs)).

## 무대 공유와 자리 되찾기

Live2D 모델과 반복 영상은 각각 **무대 하나**(`Live2DStage` / `MotionVideoStage`)를 여러 화면이 같이 씁니다. 그래서 한 화면이 다른 캐릭터를 무대에 올리면, 먼저 쓰던 화면은 `Displaced` 이벤트로 알림을 받아 **정적 일러스트로 물러납니다**(같은 텍스처를 계속 보여 주면 엉뚱한 캐릭터가 보이던 문제 — 예: 뽑기 후 픽업 배너 그림이 방금 뽑은 상시 6성으로 바뀜). 무대가 비면 `Live2DSlot`이 0.25초마다 확인해 다시 움직이는 일러스트로 되돌립니다.
