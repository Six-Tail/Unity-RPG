# 가챠 결과 UI / 연출

위치: `Assets/_Project/Scripts/Gacha/UI/`

`GachaController.OnPulled` 이벤트를 구독해 **10연 카드 공개 · 성급별 연출 · 픽업 강조**를 보여 주는 결과 화면입니다.
프리팹/씬 배치가 거의 필요 없습니다 — 컴포넌트 하나가 Canvas·카드·버튼을 코드로 만듭니다.

## 구성

| 파일 | 역할 |
|---|---|
| `GachaResultPanel` (MonoBehaviour) | 결과 화면 전체. 공개 시퀀스·스킵·픽업 강조·다시뽑기 버튼 |
| `GachaResultCard` (MonoBehaviour) | 카드 1장. 성급 색·글로우·픽업 뱃지·공개 팝 애니메이션 |
| `GachaRarityPalette` (SO) | 성급별 색/연출 세기. *Create > SubcultureRPG > Gacha > Rarity Palette* |
| `UIFactory` | 코드로 uGUI를 만드는 내부 헬퍼 |

## 세팅 (3단계)

1. 씬에 **빈 GameObject** 생성 → `GachaResultPanel` 컴포넌트 추가.
2. 인스펙터 `controller` 에 씬의 `GachaController` 연결. (비워 두면 자동 탐색)
3. `font` 에 **한글이 포함된 TMP 폰트 에셋** 연결. (아래 폰트 주의)

뽑기(`PullOnce`/`PullMulti`)를 호출하면 결과 화면이 자동으로 뜹니다. 끝.

## 폰트 / 패키지 주의

- **TMP Essentials**: 처음이면 `Window > TextMeshPro > Import TMP Essential Resources` 1회 실행.
- **한글**: 기본 TMP 폰트에는 한글이 없습니다. 한글 `.ttf` 를 넣고 *우클릭 > Create > TextMeshPro > Font Asset* 으로 만든 뒤 `font` 필드에 연결하세요. (별 글리프 ★ 포함 폰트 권장)
- **입력 시스템**: 이 프로젝트는 레거시 Input Manager 기준(`StandaloneInputModule`)입니다. 버튼이 안 눌리면 *Project Settings > Player > Active Input Handling* 을 확인하세요.

## 연출 동작

- **순차 공개**: 카드가 하나씩 팝업(scale)되며 성급 색으로 번쩍입니다.
- **성급별**: 색·글로우·플래시 세기가 성급에 비례합니다(`GachaRarityPalette.punch`).
- **픽업 강조**: 픽업 6성이 나오면 금빛 풀스크린 플래시 + `PICK UP 획득!` 배너. 일반 6성/5성은 가벼운 화면 틴트.
- **SKIP**: 우상단 버튼으로 전부 즉시 공개(최고 등급만 마무리 강조).
- **다시 뽑기**: 하단 `1회` / `10연` 버튼이 `controller` 를 다시 호출 → 결과 갱신.
- 연출은 `Time.unscaledDeltaTime` 기반이라 `Time.timeScale = 0`(일시정지) 상태에서도 재생됩니다.

## 튜닝 (인스펙터)

| 필드 | 뜻 |
|---|---|
| `revealInterval` | 카드 간 공개 간격(초) |
| `revealIntervalHigh` | 5성 이상 공개 뒤 추가 여유(초) |
| `columns` / `cellSize` / `spacing` | 그리드 열 수·셀 크기·간격 |
| `autoPlayOnPull` | 뽑을 때 자동 표시 끄기/켜기 |

성급 색·연출 세기는 `GachaRarityPalette` 에셋에서 성급별로 조정합니다(코드 수정 불필요).

## 코드에서 직접 띄우기

```csharp
[SerializeField] GachaResultPanel resultPanel;
[SerializeField] GachaController gacha;

// autoPlayOnPull 을 끄고 수동 제어할 때
void OnTenPull()
{
    var results = gacha.PullMulti();
    resultPanel.Show(results);
}
```

## 확장 포인트

- 6성 픽업 시 `CharacterViewFactory` 로 Live2D 모델을 띄우는 '대형 등장 연출'을 `EmphasizePickup` 에 연결할 수 있습니다. (현재는 썸네일 카드 강조)
- 카드 커버/글로우에 스프라이트를 쓰고 싶으면 `GachaResultCard.Build` 의 해당 `Image` 에 `sprite` 를 지정하세요.
- `GachaResultPanel.Show(results)` 는 public 이라, 연출 전용으로 직접 호출해도 됩니다.
