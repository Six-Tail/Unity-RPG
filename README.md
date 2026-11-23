# Subculture Turn-based RPG (가제)

서브컬쳐 턴제 가챠 RPG — 개인/친구용 프로젝트.
붕괴: 스타레일 · 림버스 컴퍼니 · 아우터 플레인 계열.

- **엔진**: Unity (2D) — Unity 6 LTS 권장
- **아트**: AI 이미지 생성 → 게임 에셋
  - 5성 이하 → **정적 일러스트** (Sprite)
  - 6성 → **Live2D** (Cubism)
- **플랫폼**: PC 우선 / 모바일 선택

> `subculture-rpg`는 **작업용 임시 이름**입니다. 폴더째 이름을 바꾸거나 다른 경로로 옮겨도 됩니다(새 Git 저장소라 안전).

---

## 저장소 구조 (2개로 분리)

```
D:\Users\jhbae\Downloads\
├── AI 이미지 제작 프로그램\   ← AI 이미지 도구 (별도, 건드리지 않음)
└── subculture-rpg\            ← 이 게임 (독립 Git 저장소)  ← 지금 여기
```

AI 도구와 게임은 **생명주기가 다르므로 저장소를 분리**합니다.
AI 도구로 뽑은 **결과물(이미지)만** 이 저장소의 `Assets/_Project/Art/...`로 가져옵니다.

---

## 빠른 시작

1. **Unity Hub** 설치 → **Unity 6 LTS** 설치 (모바일 빌드하려면 *Android Build Support* 포함)
2. Unity Hub → **Add project from disk** → 이 폴더(`subculture-rpg`) 선택 → 열기
   - 버전이 다르면 Hub가 업그레이드를 제안합니다 → 수락하면 됩니다.
   - 자세한 절차/문제 해결: [docs/unity-setup.md](docs/unity-setup.md)
3. Unity가 열리면서 `.meta`와 `Library/`를 자동 생성합니다. 처음 한 번 전체 커밋:

```bash
git -C "D:/Users/jhbae/Downloads/subculture-rpg" add .
```

```bash
git -C "D:/Users/jhbae/Downloads/subculture-rpg" commit -m "chore: Unity가 생성한 .meta/ProjectSettings 추가"
```

---

## 플레이하기

1. 메뉴 **`SubcultureRPG > 메인 씬 생성 (홈 허브)`** → `Assets/_Project/Scenes/Main.unity` 생성(빌드 첫 씬 등록)
2. ▶ Play → 홈에서 **스테이지 / 가챠 / 육성 / 임무 / 상점 / 가방**으로 이동 (스테이지 클리어로 금화·경험치 재료, 스테이지 화면의 특수 던전 탭에서 돌파 재료·금화·스킬 교본 획득, 상점에서 재료 구매·교환)

시스템별 문서:
[메인 허브](docs/main-hub.md) · [스테이지·보상](docs/stages.md) · [금화·특수 던전](docs/economy.md) · [상점](docs/shop.md) · [임무](docs/missions.md) · [장비](docs/equipment.md) · [가챠](docs/gacha.md) · [가챠 화면](docs/gacha-screen.md) · [가챠 결과 UI](docs/gacha-ui.md) ·
[전투](docs/battle.md) · [전투 UI](docs/battle-ui.md) · [가챠↔전투 연동](docs/gacha-battle-link.md) · [육성](docs/growth.md) · [캐릭터 특성·정렬/필터](docs/characters.md) · [합성·초월](docs/transcendence.md) · [폰트](docs/fonts.md) · [이미지 에셋](docs/art.md) · [이미지 프롬프트 목록](docs/art-prompts.md)

---

## 어디에 뭐가 있나

- `Assets/_Project/` — 우리가 만드는 모든 것 (언더스코어로 맨 위 정렬)
- `Assets/_Project/Scripts/Characters/` — **성급별 캐릭터 표시 로직 (작성 완료)**
  - `Rarity.cs` — 성급 enum + `UsesLive2D()` (6성만 Live2D, 기준 한 곳)
  - `ICharacterView.cs` — 전투/연출이 정적·Live2D를 구분 안 해도 되게 하는 공통 인터페이스
  - `StaticCharacterView.cs` — 5성 이하: Sprite 표시
  - `Live2DCharacterView.cs` — 6성: Live2D 표시 (SDK 임포트 후 채울 스텁)
  - `CharacterViewFactory.cs` — 성급 보고 알맞은 뷰 자동 생성
  - `CharacterData.cs` — 캐릭터 데이터(ScriptableObject)
- 전체 폴더 설명: [docs/folder-structure.md](docs/folder-structure.md)

---

## Git / LFS

- `.gitignore`, `.gitattributes` 설정 완료 (Unity + Git LFS + Smart Merge).
- **이미지·오디오·Live2D(.moc3) 등 대용량 바이너리는 자동으로 LFS**에 저장됩니다.
- 저장소마다 한 번(이미 실행됨):

```bash
git -C "D:/Users/jhbae/Downloads/subculture-rpg" lfs install
```

---

## AI 이미지 → Live2D 주의

- **5성 이하 정적 일러스트**: AI 출력물을 그대로 Sprite로 → 거의 공짜.
  - `Assets/_Project/Resources/Art/`에 정해진 파일명으로 넣으면 자동 연결됩니다. 일괄 생성 스크립트·프롬프트: [docs/art.md](docs/art.md)
- **6성 Live2D**: AI는 *평면 1장*을 주지만 Live2D는 *레이어 분리 PSD*가 필요
  → 레이어 분리 → Cubism Editor 리깅 → `.moc3` 내보내기 → Unity 임포트.
  → 그래서 **6성만 Live2D**로 한정한 설계가 작업량 면에서 합리적입니다.
