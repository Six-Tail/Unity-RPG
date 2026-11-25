# Unity 세팅 가이드

## 1. 설치

1. [Unity Hub](https://unity.com/download) 설치.
2. Unity Hub → **Installs** → **Install Editor** → **Unity 6 LTS** 설치.
   - 모바일 빌드 예정이면 **Android Build Support** (및 iOS는 Mac 필요) 체크.
   - 2D 게임이라 그래픽 옵션은 기본값으로 충분합니다.

## 2. 프로젝트 열기 (권장: 바로 열기)

1. Unity Hub → **Projects** → **Add** → **Add project from disk**.
2. 이 폴더 `D:\Users\jhbae\Downloads\subculture-rpg` 선택.
3. 열기.
   - `ProjectVersion.txt`는 **Unity 6000.0.23f1** 기준입니다. 설치한 버전이 다르면:
     - Hub가 **업그레이드 제안** → 수락하면 됩니다(권장).
     - 또는 이 파일의 버전 문자열을 설치한 버전으로 바꿔도 됩니다.
   - Package Manager 경고가 떠도 프로젝트는 열립니다. 뜨면 **Window > Package Manager**에서 해당 패키지를 업데이트/제거하세요.
4. 첫 임포트가 끝나면 Unity가 `.meta`와 `Library/`를 생성합니다.

### 2D 모드 확인
- Scene 뷰 상단의 **2D** 토글이 켜져 있는지 확인.
- 이미지를 캐릭터로 쓰려면 임포트 설정의 **Texture Type = Sprite (2D and UI)** 로 둡니다(프로젝트 기본값으로 지정 가능).

### (선택) 패키지 추가
필요해지면 **Window > Package Manager**에서:
- `2D Animation` — 스프라이트 스키닝/간이 본 애니메이션
- `Input System` — 입력
- `TextMeshPro` — 텍스트(최근 Unity는 UGUI에 포함)

## 3. 첫 커밋 (.meta 포함)

Unity가 생성한 `.meta`/`ProjectSettings`를 커밋합니다.

```bash
git -C "D:/Users/jhbae/Downloads/subculture-rpg" add .
git -C "D:/Users/jhbae/Downloads/subculture-rpg" commit -m "chore: Unity가 생성한 .meta/ProjectSettings 추가"
```

> **중요**: `.meta` 파일은 항상 커밋하세요. 에셋 이동/참조가 `.meta`의 GUID로 연결됩니다.

## 4. Git LFS 확인

이미 `git lfs install` 완료. 아트/오디오를 넣기 **전에** 동작 확인:

```bash
git -C "D:/Users/jhbae/Downloads/subculture-rpg" lfs track
```

PNG 하나를 넣고 커밋한 뒤, 아래로 LFS에 올라갔는지 확인:

```bash
git -C "D:/Users/jhbae/Downloads/subculture-rpg" lfs ls-files
```

## 5. (선택) Smart Merge 드라이버 등록

씬/프리팹 충돌을 Unity가 자동 병합하도록 `.gitattributes`에 적어둔 `unityyamlmerge` 드라이버를 등록합니다. 설치 경로는 버전에 맞게 바꾸세요.

```bash
git -C "D:/Users/jhbae/Downloads/subculture-rpg" config merge.unityyamlmerge.name "Unity SmartMerge"
git -C "D:/Users/jhbae/Downloads/subculture-rpg" config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.0.23f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p %O %B %A %A'
git -C "D:/Users/jhbae/Downloads/subculture-rpg" config merge.unityyamlmerge.recursive binary
```

> 등록하지 않아도 됩니다. 그 경우 충돌 시 일반 텍스트 머지로 폴백합니다(덜 똑똑할 뿐).

## 대안: 2D 템플릿으로 깔끔하게 시작하고 싶다면

Hub의 2D 템플릿 기본값(2D 물리/스프라이트 프리셋 등)을 그대로 쓰고 싶으면:

1. Unity Hub → **New project** → **2D** 템플릿 → 임시 위치(예: `Desktop/sc-tmp`)에 생성, 한 번 열고 닫기.
2. 생성된 프로젝트의 파일을 이 저장소로 복사(우리 `_Project`/git 파일은 보존):

```bash
SRC="/c/Users/jhbae/Desktop/sc-tmp"; DST="/d/Users/jhbae/Downloads/subculture-rpg"
cp -rf "$SRC/Packages/." "$DST/Packages/"
cp -rf "$SRC/ProjectSettings/." "$DST/ProjectSettings/"
cp -rn "$SRC/Assets/." "$DST/Assets/"
```

3. 이 폴더(`subculture-rpg`)를 Unity Hub에 **Add project from disk**로 열기.
4. 임시 폴더 삭제 후 커밋.

> 경로는 본인 환경에 맞게 바꾸세요. 어디에 만들었는지 알려주면 이 복사를 대신 해드릴 수 있습니다.

## 화면 비율

모든 화면의 `CanvasScaler`는 1920×1080 기준 **Expand** 모드입니다. 어떤 창 크기·비율에서도 1920×1080 설계 영역이 전부 보이고, 남는 쪽(가로로 긴 화면은 좌우, 세로로 긴 화면은 위아래)은 여백이 됩니다. 좌우 끝에 붙던 화면(가방 · 상점 · 도감)은 `UIFactory.Frame`으로 가운데 1920 폭에 모았고, 목록은 `UIFactory.MakeVerticalScroll`로 스크롤됩니다(육성 목록 · 스테이지 노드 · 스테이지 상세 본문 포함).

## 입력 시스템

- **Input System 패키지 전용**(Player 설정 `Active Input Handling` = Input System Package (New)). 옛 Input Manager는 쓰지 않습니다.
- UI 클릭 · 터치는 화면마다 EventSystem을 코드로 만들 때 [UIInput.cs](../Assets/_Project/Scripts/UI/UIInput.cs)가 `InputSystemUIInputModule` + 기본 UI 조작(`AssignDefaultActions`)을 붙입니다. 새 화면을 만들 때도 `UIInput.AddModule(es)` 또는 `UIInput.Ensure()`를 쓰세요.
- 코드에서 `UnityEngine.Input`(Input.GetKey 등)을 쓰면 오류가 나므로, 키 입력이 필요하면 `UnityEngine.InputSystem`(Keyboard.current 등)을 쓰세요.
