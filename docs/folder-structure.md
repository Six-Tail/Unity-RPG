# 폴더 구조

```
subculture-rpg/
├── .gitignore              # Unity용 무시 규칙
├── .gitattributes          # Git LFS(대용량 바이너리) + Smart Merge
├── README.md
├── docs/
│   ├── folder-structure.md # (이 문서)
│   └── unity-setup.md      # Unity 설치·열기·LFS·머지 세팅
├── Packages/
│   └── manifest.json       # Unity 패키지 목록 (Unity 6 기준, 최소)
├── ProjectSettings/
│   └── ProjectVersion.txt  # Unity 버전 (열 때 자동 보정/업그레이드)
└── Assets/
    ├── Plugins/            # 서드파티(Live2D Cubism SDK 등)
    └── _Project/           # ★ 우리가 만드는 모든 것 (_ 로 맨 위 정렬)
        ├── Art/
        │   ├── Characters/
        │   │   ├── Static/     # 5성 이하 정적 일러스트 (AI PNG)
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
