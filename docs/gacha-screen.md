# 가챠 뽑기 화면

위치: `Assets/_Project/Scripts/Meta/PlayerWallet.cs` + `Meta/UI/GachaScreen.cs`

뽑기 홈 화면 — 재화·천장 표시 + 1회/10연 뽑기 → 결과 연출 → 로스터 반영.

## 구성

| 파일 | 역할 |
|---|---|
| `GachaScreen` (MonoBehaviour) | 뽑기 화면 — 픽업 미리보기·크리스탈·천장·1회/10연 버튼 |
| `PlayerWallet` (MonoBehaviour) | 재화(크리스탈) 지갑. PlayerPrefs 저장 |
| (기존) `GachaController` | 확률/천장 뽑기 엔진 |
| (기존) `GachaResultPanel` | 결과 카드 공개 연출(`OnPulled` 자동 표시) |
| (기존) `PlayerRoster` | 뽑은 캐릭터 자동 보유 |

## 흐름

1. **뽑기 화면**: 크리스탈 잔액 + 6성 천장 카운터 + 픽업 캐릭터 미리보기.
2. **1회/10연 버튼** → `PlayerWallet.Spend(비용)` → `GachaController.PullOnce/PullMulti`.
3. **결과 연출**: `GachaResultPanel`이 `OnPulled`로 자동 표시(카드 공개·성급별·픽업 강조).
4. **보유 반영**: `PlayerRoster`가 `OnPulled`로 자동 보유 + 저장 → 연동 데모 파티 편성에도 등장.

## 빠른 체험

메뉴 **`SubcultureRPG > Demo > 가챠 뽑기 데모 생성`** → `GachaDemo` 씬 → ▶ Play.
- 크리스탈 16000 지급 (1회 160 / 10연 1600, '충전' 버튼 있음).
- 뽑으면 결과가 연출되고, 캐릭터가 로스터(`PlayerPrefs` "roster")에 저장됩니다.
- 같은 저장소를 쓰므로 **연동 데모(LinkDemo)의 파티 편성**에서 여기서 뽑은 캐릭터가 보입니다.

## 비용 튜닝 (인스펙터)

| 필드 | 뜻 |
|---|---|
| `singleCost` / `multiCost` | 1회/10연 비용 |
| `chargeAmount` | 테스트 충전 버튼 지급량 |
| `PlayerWallet.startingCrystals` | 시작 크리스탈 |

## 참고 / 확장

- 1회/10연 뽑기와 결과 화면의 `다시 뽑기`는 모두 **크리스탈 사용 확인 창**을 거칩니다(확인을 눌러야 차감·뽑기). 연출은 [gacha-ui.md](gacha-ui.md).
- **천장 보장**: `GachaController`가 PlayerPrefs에 배너별로 저장(이어서 뽑기).
- 확장: 재화 획득(스테이지 보상), 픽업 교체, 뽑기 전 대형 연출 등.
