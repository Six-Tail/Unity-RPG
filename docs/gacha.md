# 가챠(확률 / 천장) 시스템

위치: `Assets/_Project/Scripts/Gacha/`

## 구성

| 파일 | 역할 |
|---|---|
| `GachaConfig` (SO) | 확률·천장 수치. *Create > SubcultureRPG > Gacha > Gacha Config* |
| `GachaBanner` (SO) | 픽업 + 등급별 풀. *Create > SubcultureRPG > Gacha > Banner* |
| `GachaPityState` | 배너별 천장 카운터(세이브 대상) |
| `GachaResult` | 뽑기 1회 결과 |
| `GachaMachine` | 순수 로직 엔진(RNG 주입 가능 → 테스트 용이) |
| `GachaController` (MonoBehaviour) | 씬에 붙여 쓰는 진입점(세이브/로드, 1회·10연) |

## 기본 수치 (전부 튜닝 가능)

| 등급 | 기본 확률 | 천장 |
|---|---|---|
| 6성 | 0.6% | 소프트 74부터 +6%/뽑기, **하드 90 확정** |
| 5성 | 5.1% | **10연 내 5성 이상 보장** |
| 4성 | 30% | - |
| 3성 | 나머지(64.3%) | - |

- **픽업(50/50)**: 6성 획득 시 50% 확률로 픽업. 실패하면 다음 6성은 픽업 확정. (5성도 동일 방식, 선택)

## 세팅 (에디터)

1. **GachaConfig** 에셋 1개 생성 → 확률/천장 조정.
2. **Banner** 에셋 생성 → `config` 연결, `featured6`/`featured5`(픽업)와 `pool6~pool3`(비픽업)에 `CharacterData` 넣기.
3. 빈 GameObject에 **GachaController** 컴포넌트 추가 → `banner` 연결.

## 사용 (코드)

```csharp
[SerializeField] GachaController gacha;

void Start() {
    gacha.OnPulled += Show; // 결과 연출 구독
}

void OnClickSingle() => gacha.PullOnce();
void OnClickTen()    => gacha.PullMulti();   // 기본 10연

void Show(List<GachaResult> results) {
    foreach (var r in results)
        Debug.Log($"{r.rarity} / {r.character?.displayName} / 픽업={r.isFeatured}");
}
```

## 천장 동작 메모

- 카운터는 매 뽑기 +1, 해당 등급(이상) 획득 시 0으로 리셋.
- 6성 획득은 5성 카운터도 리셋(5성 '이상' 보장이므로).
- 세이브는 `PlayerPrefs`에 배너별로 저장. 인스펙터에서 컴포넌트 우클릭 → **"천장 상태 초기화"**.

## 테스트 팁

`GachaMachine`에 고정 시드 RNG를 주입하면 결정적 검증이 가능합니다.

```csharp
var seeded = new System.Random(12345);
var machine = new GachaMachine(() => (float)seeded.NextDouble());
// 90연에서 6성 확정되는지, 10연마다 5성+ 나오는지 등 검증
```
