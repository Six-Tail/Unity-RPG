# 폴더 구조

```
subculture-rpg/
├── .gitignore              # Unity용 무시 규칙
├── .gitattributes          # Git LFS(대용량 바이너리) + Smart Merge
├── README.md
├── docs/
│   ├── folder-structure.md # (이 문서)
│   └── unity-setup.md      # Unity 설치·열기·LFS·머지 세팅
├── tools/
│   └── art/                # 이미지 목록·프롬프트(art-manifest.js) + 일괄 생성 스크립트(generate-art.js)
├── Packages/
│   └── manifest.json       # Unity 패키지 목록 (Unity 6 기준, 최소)
├── ProjectSettings/
│   └── ProjectVersion.txt  # Unity 버전 (열 때 자동 보정/업그레이드)
└── Assets/
    ├── Plugins/            # 서드파티(Live2D Cubism SDK 등)
    └── _Project/           # ★ 우리가 만드는 모든 것 (_ 로 맨 위 정렬)
        ├── Resources/
        │   └── Art/            # ★ 게임이 파일명으로 찾아 쓰는 이미지 (docs/art.md)
        │       ├── Characters/ # 캐릭터 일러스트 {id}.png
        │       ├── Thumbs/     # 캐릭터 썸네일
        │       ├── Enemies/    # 적 {이름}.png
        │       ├── Items/      # 재료 아이콘
        │       ├── Equipment/  # 장비 부위 아이콘
        │       ├── Backgrounds/# 화면·전투 배경
        │       └── Banners/    # 가챠 배너 그림
        ├── Art/                # 원본·작업 파일(게임이 직접 읽지 않음)
        │   ├── Characters/
        │   │   ├── Static/     # 일러스트 원본(PSD 등)
        │   │   └── Live2D/     # 6성 Live2D (.moc3/.model3.json/텍스처)
        │   ├── Backgrounds/
        │   ├── UI/
        │   └── Icons/
        ├── Audio/
        │   ├── BGM/
        │   └── SFX/
        ├── Prefabs/           # 캐릭터/UI/이펙트 프리팹 (6성 Live2D 프리팹 포함)
        ├── Scenes/
        ├── ScriptableObjects/
        │   └── Characters/    # CharacterData 에셋 생성 위치
        └── Scripts/
            ├── Characters/    # 성급별 표시 로직 (작성 완료)
            ├── Battle/        # 턴제 전투
            ├── Gacha/         # 가챠(확률/천장)
            ├── Core/          # 세이브/매니저/유틸
            └── UI/            # 화면/팝업
```

## 원칙

- **`Assets/_Project/` 안에만** 우리 작업물을 둡니다. 에셋스토어/서드파티는 밖(`Assets/Plugins` 등)에 두면 업데이트·삭제가 쉽습니다.
- `Library/`, `Temp/`, `Logs/`, IDE 파일은 Git에 올리지 않습니다 (`.gitignore`).
- **`.meta` 파일은 반드시 Git에 올립니다** (에셋과 1:1 대응, 빠지면 참조가 깨집니다).
- 대용량 바이너리(png/psd/moc3/wav/mp3 등)는 자동으로 **Git LFS**로 갑니다 (`.gitattributes`).
